using Microsoft.Xna.Framework;
using MonoGame.Extended;
using MonoGame.Extended.Entities;
using MonoGame.Extended.Entities.Systems;
using MonoGame.Extended.Shapes;
using MonoGame.Extended.Tiled;
using Platformer.Components;
using System;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using static System.Formats.Asn1.AsnWriter;

namespace Platformer.Systems
{
    internal class TileMapSystem : EntityUpdateSystem
    {
        private const ushort PPT = 32; //Pixels Per Tile
        private const float GroundBuffer = 2f;
        private const float MARGIN = 1 / 256f;

        private ComponentMapper<Physics> _physicsMapper;
        private ComponentMapper<Transform2> _transformMapper;
        private ComponentMapper<IShapeF> _shapeMapper;

        private TiledMapTileLayer _collisionLayer;
        private TiledMap _tiledMap;

        public TileMapSystem(TiledMap tiledMap) : base(Aspect.All(typeof(Transform2), typeof(Physics), typeof(IShapeF)))
        {
            _tiledMap = tiledMap;
            _collisionLayer = tiledMap.TileLayers.Single(l => l.Name == "Collision");
        }

        public override void Initialize(IComponentMapperService mapperService)
        {
            _physicsMapper = mapperService.GetMapper<Physics>();
            _transformMapper = mapperService.GetMapper<Transform2>();
            _shapeMapper = mapperService.GetMapper<IShapeF>();
        }

        public TiledMapTile? getTile(ushort x, ushort y)
        {
            if (0 <= x && x < _collisionLayer.TileWidth && 0 <= y && y < _collisionLayer.TileHeight)
                return _collisionLayer.GetTile(x, y);
            else
                return null;
        }

        public TiledMapTile? getTile(float x, float y)
        {
            ushort tx = (ushort)MathF.Floor(x / PPT); //tile's x index
            ushort ty = (ushort)MathF.Floor(y / PPT); //tile's y index

            return getTile(tx, ty);
        }

        public override void Update(GameTime gameTime)
        {
            foreach (int entity in ActiveEntities)
            {
                var p = _transformMapper.Get(entity).Position;
                var v = _physicsMapper.Get(entity).Velocity;
                RectangleF box = GetBoundingRectangle(_shapeMapper.Get(entity));
                Point2 center = box.Center;

                bool inSlope = false;
                TiledMapTile? currentTile = getTile(center.X, p.Y);
                if (currentTile.HasValue)
                {
                    inSlope = currentTile.Value.GlobalIdentifier == TileID.SLOPE || currentTile.Value.GlobalIdentifier == TileID.HALF_SLOPE;
                }

                float newX = -1;
                float x0 = p.X;
                float x1 = p.X + v.X;
                if (x0 < x1)
                {
                    x0 += box.Width;
                    x1 += box.Width;
                }

                _physicsMapper.Get(entity).grounded = false;
                //do horizontal collision checks all the way up the height of the entity's hitbox
                //round up the bottom and the height to the nearest tile
                float h = box.Height;
                float ceil = MathF.Ceiling(h / PPT);
                for (int i = inSlope ? 1 : 0; i <= ceil; i++)
                {
                    float y = p.Y + MathF.Min(h, i * PPT);
                    float tx = horizontalCheck(x0, x1, y);
                    if (MathF.Abs(x0 - tx) < MathF.Abs(x0 - newX))
                        newX = tx;
                }

                if (newX >= 0)
                {
                    p.X = newX - (x0 < x1 ? box.Width : 0);
                    v.X = 0;
                }

                float newY = -1;
                float y0 = p.Y;
                float y1 = p.Y + v.Y;
                if (y0 < y1)
                {
                    y0 += box.Height;
                    y1 += box.Height;
                }

                float w = box.Width;
                ceil = MathF.Ceiling(w / PPT);
                for (int i = 0; i <= ceil; i++)
                {
                    float x = inSlope ? center.X : p.X + MathF.Min(w, i * PPT);
                    Tuple<int, float> collision = verticalChecks(x, center.X, y0, y1);
                    float ty = collision.Item2;
                    if (collision.Item1 == TileID.SLOPE || collision.Item1 == TileID.HALF_SLOPE)
                    {
                        newY = ty;
                    }
                    else if (MathF.Abs(y0 - ty) < MathF.Abs(y0 - newY))
                        newY = ty;


                    if (v.Y < 0 && isSolid(x, p.Y + GroundBuffer))
                        _physicsMapper.Get(entity).grounded = true;
                }

                if (newY >= 0)
                {
                    p.Y = newY - (y0 < y1 ? box.Height : 0);
                    v.Y = 0;
                }
            }
        }

        private RectangleF GetBoundingRectangle(IShapeF shape)
        {
            if (shape.GetType() == typeof(CircleF))
            {
                CircleF circle = (CircleF) shape;
                RectangleF rect = new RectangleF(circle.Center.X - circle.Radius, circle.Center.Y - circle.Radius, circle.Diameter, circle.Diameter);
            }
            if (shape.GetType() == typeof(RectangleF))
            {
                return (RectangleF)shape;
            }
            if (shape.GetType() == typeof(EllipseF))
            {
                EllipseF ellipse = (EllipseF)shape;
                return ellipse.BoundingRectangle;
            }

            Debug.LogInfo(typeof(Shape) + " has no implemented Bounding Rectangle!");
            return new RectangleF();
        }

        private Tuple<int, float> verticalChecks(float x, float centerX, float y0, float y1)
        {
            float ty = -1;

            foreach (float y in new float[] { y0, y1 })
            {
                var test = getTile(x, y);
                if (test.HasValue)
                {
                    var tile = test.Value;
                    RectangleF box = new RectangleF(tile.X, tile.Y, _tiledMap.TileWidth, _tiledMap.TileHeight);
                    float top = box.Y + box.Height;
                    switch (tile.GlobalIdentifier)
                    {
                        case TileID.FULL:
                            if (y < box.Center.Y) ty = box.Y - MARGIN;
                            else ty = top + MARGIN;
                            break;
                        case TileID.HALF_FULL:
                            if (box.y + box.height / 2 <= y && y <= top)
                                ty = top + MARGIN;
                            if (box.y <= y && y < box.y + box.height / 2)
                                ty = box.Y - MARGIN;
                            break;
                        case TileID.PLATFORM:
                            if (y1 <= top && top <= y0)
                                ty = top + MARGIN;
                            break;
                        case TileID.HALF_SLOPE:
                        case TileID.SLOPE:
                            float xs = box.X + tile.X,
                                    ys = box.Y + tile.GlobalIdentifier.y0,
                                    m = (tile.flipX ^ tile.flipY ? -1 : 1) * tile.Value.GlobalIdentifier.m;

                            if (box.X <= centerX && centerX < box.X + box.Width)
                            {
                                ty = m * (centerX - xs) + ys;
                                if (y1 > ty + 5 && !tile.flipY)
                                    ty = -1;
                                if (y1 < ty && tile.flipY)
                                    ty = -1;
                            }
                            break;
                    }
                    if (ty >= 0)
                        return new Tuple<int, float>(tile.Value.GlobalIdentifier, ty);
                }
            }
            return new Tuple<int, float>(-1, ty);
        }

        private float horizontalCheck(float x0, float x1, float y)
        {
            float tx = -1;

            Entity test = getTileExcludeX(x1, y);
            if (test != null)
            {
                TileComponent tile = tiles.get(test);
                Rectangle box = Rectangle.tmp2.set(hitboxes.get(test).setPosition(positions.get(test)));
                switch (tile.Value.GlobalIdentifier)
                {
                    case SLOPE:
                    case HALF_SLOPE:
                        if (box.contains(x1, y))
                        {
                            if (tile.flipX)
                            {
                                if (x0 <= box.X)
                                    tx = box.X - MARGIN;
                            }
                            else
                            {
                                if (x0 >= box.X + box.Width)
                                    tx = box.X + box.Width;
                            }
                        }
                        break;
                    case HALF_FULL:
                        if (box.contains(x1, y))
                        {
                            if (x0 <= box.X)
                                tx = box.X - MARGIN;
                            if (x0 >= box.X + box.Width)
                                tx = box.X + box.Width;
                        }
                        break;
                    case FULL:
                        if (x0 >= box.X + box.Width)
                            tx = box.X + box.Width;
                        if (x0 <= box.X)
                            tx = box.X - MARGIN;
                        break;
                }
                if (tx >= 0) return tx;

            }
            return tx;
        }

        private Entity getTileExcludeX(float x, float y)
        {
            Entity tile = getTile(x, y);

            if (tile != null)
            {
                Rectangle box = Rectangle.tmp2.set(hitboxes.get(tile).setPosition(positions.get(tile)));
                if (x > box.X && x < box.X + box.width && y >= box.y && y <= box.y + box.height)
                    return tile;
            }
            return null;
        }

        public bool isSolid(float x, float y)
        {
            Entity entity = getTile(x, y);
            if (entity == null) return false;

            TileComponent tile = tiles.get(entity);
            HitboxComponent box = hitboxes.get(entity);
            switch (tile.Value.GlobalIdentifier)
            {
                case FULL:
                    return true;
                case HALF_FULL:
                case PLATFORM:
                    return box.contains(x, y);
                case HALF_SLOPE:
                case SLOPE:
                    float xs = box.X + tile.Value.GlobalIdentifier.X0,
                            ys = box.Y + tile.Value.GlobalIdentifier.y0,
                            m = (tile.flipX ^ tile.flipY ? -1 : 1) * tile.Value.GlobalIdentifier.m;

                    float boundY = m * (x - xs) + ys;
                    return tile.flipY ^ y <= boundY;
            }
            return false;
        }
    }
}

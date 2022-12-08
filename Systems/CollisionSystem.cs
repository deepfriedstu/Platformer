using Microsoft.Xna.Framework;
using MonoGame.Extended;
using MonoGame.Extended.Entities;
using MonoGame.Extended.Entities.Systems;
using MonoGame.Extended.Shapes;
using MonoGame.Extended.Tiled;
using Platformer.Components;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Platformer
{
    internal class CollisionSystem : EntityUpdateSystem
    {
        private ComponentMapper<Physics> _physicsMapper;
        private ComponentMapper<Transform2> _transformMapper;
        private ComponentMapper<IShapeF> _shapeMapper;
        private ComponentMapper<Polygon> _polygonMapper;
        private ComponentMapper<Polyline> _polylineMapper;

        private TiledMap _tiledMap;
        private TiledMapTileLayer _collisionLayer;

        public CollisionSystem(TiledMap tiledMap) : base(Aspect.All(typeof(Physics), typeof(Transform2)).One(typeof(IShapeF), typeof(Polygon), typeof(Polyline)))
        {
            _tiledMap = tiledMap;
            _collisionLayer = tiledMap.TileLayers.Single(l => l.Name == "Collision");
        }

        public override void Initialize(IComponentMapperService mapperService)
        {
            _physicsMapper = mapperService.GetMapper<Physics>();
            _transformMapper = mapperService.GetMapper<Transform2>();
            _shapeMapper = mapperService.GetMapper<IShapeF>();
            _polygonMapper = mapperService.GetMapper<Polygon>();
            _polylineMapper = mapperService.GetMapper<Polyline>();
        }

        public override void Update(GameTime gameTime)
        {
            for(int i = 0; i < ActiveEntities.Count; i++)
            {
                int entityA = ActiveEntities.ElementAt(i);
                IShapeF shapeA = _shapeMapper.Get(entityA);

                //Collisions with other entities
                for(int o = i + 1; o < ActiveEntities.Count; o++)
                {
                    int entityB = ActiveEntities.ElementAt(o);

                    IShapeF shapeB = _shapeMapper.Get(entityB);

                    if(shapeA.Intersects(shapeB))
                    {
                        Collision(entityA, entityB);
                        Collision(entityB, entityA);
                    }
                }

                //Collision with the TiledMap
                RectangleF extents = GetBoundingRectangle(entityA);
                Vector2 velocity = _physicsMapper.Get(entityA).Velocity;

                if(MathF.Abs(velocity.X) > 0)
                {
                    float x = velocity.X > 0 ? extents.Right : extents.Left;

                    for(float y = extents.Top; y < extents.Bottom; y += _tiledMap.TileHeight)
                    {
                        ushort tileX = (ushort)MathF.Floor(x / _tiledMap.TileWidth);
                        ushort tileY = (ushort)MathF.Floor(y / _tiledMap.TileHeight);

                        TiledMapTile tile = _collisionLayer.GetTile(tileX, tileY);
                        
                        TiledMapTileset tileset = _tiledMap.GetTilesetByTileGlobalIdentifier(tile.GlobalIdentifier);
                        //Once you have identified the tileset,
                        //subtract its firstgid from the tile’s GID to get the local ID of the tile within the tileset.
                        //https://doc.mapeditor.org/en/stable/reference/global-tile-ids/
                        int tilesetId = _tiledMap.GetTilesetFirstGlobalIdentifier(tileset);
                        int tileLocalId = tile.GlobalIdentifier - tilesetId;
                        var tilesetTile = tileset.Tiles.Single(t => t.LocalTileIdentifier == tileLocalId);

                        List<TiledMapPolygonObject> polygons = tilesetTile.Objects.OfType<TiledMapPolygonObject>().ToList();
                        foreach(TiledMapPolygonObject tiledPolygon in polygons)
                        {
                            Vector2[] vertices = new Vector2[tiledPolygon.Points.Length];
                            for(int p = 0; p < tiledPolygon.Points.Length; p++)
                            {
                                vertices[p] = new Vector2(tiledPolygon.Points[p].X + tile.X, tiledPolygon.Points[p].Y + tile.Y);
                            }
                            Polygon polygon = new Polygon(vertices);

                            //TODO discern why polygons are not a shape and have no intersection stuff
                        }
                    }
                }
                for(float x = extents.Left; x < extents.Right; x += _tiledMap.TileWidth)
                {
                }
            }    
        }

        private void Collision(int entityA, int entityB)
        {
            Debug.LogInfo("entity " + entityA + " collided with entity " + entityB);
        }

        private RectangleF GetBoundingRectangle(int entity)
        {
            if(_shapeMapper.Has(entity))
            {
                IShapeF shape = _shapeMapper.Get(entity);
                if (shape.GetType() == typeof(CircleF))
                {
                    CircleF circle = (CircleF)shape;
                    return new RectangleF(circle.Center.X - circle.Radius, circle.Center.Y - circle.Radius, circle.Diameter, circle.Diameter);
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
            }
            if(_polygonMapper.Has(entity))
            {
                return _polygonMapper.Get(entity).BoundingRectangle;
            }
            if(_polylineMapper.Has(entity))
            {
                return _polylineMapper.Get(entity).BoundingRectangle;
            }

            Debug.LogInfo("entity " + entity + " has no implemented Bounding Rectangle!");
            return new RectangleF();
        }
    }
}

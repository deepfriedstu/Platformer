using Microsoft.Xna.Framework;
using MonoGame.Extended;
using MonoGame.Extended.Entities;
using MonoGame.Extended.Entities.Systems;
using Platformer.Components;
using System.Collections.Generic;

namespace Platformer
{
    internal class PhysicsSystem : EntityUpdateSystem
    {
        public Vector2 RIGHT = new Vector2(1, 0);
        public Vector2 UP = new Vector2(1, 0);
        public Vector2 Gravity = new Vector2(0, 4);
        public Vector2 TerminalVelocity = new Vector2(12.4f, 12.4f);

        internal const float minGroundNormalY = 0.2f;
        internal const float shellRadius = 1 / 32f;
        protected const float minMoveDistance = 1 / 512f;

        protected RaycastHit[] hitBuffer = new RaycastHit[8];
        protected List<RaycastHit> hitList = new List<RaycastHit>(8);

        private ComponentMapper<Transform2> _transforms;
        private ComponentMapper<Physics> _physics;

        public PhysicsSystem() : base(Aspect.All(typeof(Physics), typeof(Transform2)))
        {
        }

        public override void Initialize(IComponentMapperService mapperService)
        {
            _transforms = mapperService.GetMapper<Transform2>();
            _physics = mapperService.GetMapper<Physics>();
        }

        public override void Update(GameTime gameTime)
        {
            foreach(var entity in ActiveEntities)
            {
                _physics.Get(entity).grounded = false;
                UpdateVelocity(gameTime.GetElapsedSeconds(), entity);
                UpdatePosition(gameTime.GetElapsedSeconds(), entity);
            }
        }

        private void UpdateVelocity(float seconds, int entity)
        {
            var velocity = _physics.Get(entity).Velocity;
            var acceleration = _physics.Get(entity).Acceleration;

            velocity += acceleration * seconds;
            velocity += Gravity * seconds;

            if (velocity.Y > TerminalVelocity.Y)
                velocity.SetY(TerminalVelocity.Y);
            if (velocity.X > TerminalVelocity.X)
                velocity.SetX(TerminalVelocity.X);

            _physics.Get(entity).Velocity = velocity;
        }

        private void UpdatePosition(float seconds, int entity)
        {
            var velocity = _physics.Get(entity).Velocity;
            var position = _transforms.Get(entity).Position;

            position += velocity * seconds;

            _transforms.Get(entity).Position = position;
        }
    }
}

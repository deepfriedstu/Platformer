using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoGame.Extended;
using MonoGame.Extended.Entities;
using MonoGame.Extended.Sprites;
using Platformer.Components;
using Platformer.Systems;

namespace Platformer
{
    public class Platformer : Game
    {
        private GraphicsDeviceManager _graphics;
        private SpriteBatch _spriteBatch;
        private World _world;

        private Entity _ball;

        public Platformer()
        {
            _graphics = new GraphicsDeviceManager(this);
            Content.RootDirectory = "Content";
            IsMouseVisible = true;
        }

        protected override void Initialize()
        {
            // TODO: Add your initialization logic here

            _world = new WorldBuilder()
                .AddSystem(new RenderSystem(GraphicsDevice))
                .AddSystem(new PlayerInputSystem(this))
                .AddSystem(new PhysicsSystem())
                .Build();

            _ball = _world.CreateEntity();

            base.Initialize();
        }

        protected override void LoadContent()
        {
            _spriteBatch = new SpriteBatch(GraphicsDevice);

            _ball.Attach(new Sprite(Content.Load<Texture2D>("ball")));
            _ball.Attach(new Transform2());
            _ball.Attach(new Physics());
            _ball.Attach(new KeyboardMapping());
            
        }

        protected override void Update(GameTime gameTime)
        {
            _world.Update(gameTime);

            base.Update(gameTime);
        }

        protected override void Draw(GameTime gameTime)
        {
            GraphicsDevice.Clear(Color.CornflowerBlue);

            _world.Draw(gameTime);

            base.Draw(gameTime);
        }
    }
}
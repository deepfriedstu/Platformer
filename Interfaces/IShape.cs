using MonoGame.Extended;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Platformer.Interfaces
{
    public interface IShape
    {
        public abstract RectangleF GetBoundingRectangle();
    }
}

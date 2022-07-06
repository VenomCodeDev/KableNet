using System;
using System.Collections.Generic;
using System.Text;

namespace KableNet.Math
{
    public class Vec2f
    {
        public float x, y;
        public Vec2f( float x, float y )
        {
            this.x = x;
            this.y = y;
        }

        public static Vec2f Zero { get { return new Vec2f( 0, 0 ); } }

        public override string ToString( )
        {
            return $"({x},{y})";
        }
    }
}

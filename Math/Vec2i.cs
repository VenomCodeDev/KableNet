using System;
using System.Collections.Generic;
using System.Text;

namespace KableNet.Math
{
    public class Vec2i
    {
        public int x, y;
        public Vec2i( int x, int y )
        {
            this.x = x;
            this.y = y;
        }

        public static Vec2i Zero { get { return new Vec2i( 0, 0 ); } }

        public override string ToString( )
        {
            return $"({x},{y})";
        }
    }
}

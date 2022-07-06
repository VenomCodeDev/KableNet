using System;
using System.Collections.Generic;
using System.Text;

namespace KableNet.Math
{
    public class Vec3i
    {
        public int x, y, z;
        public Vec3i( int x, int y, int z )
        {
            this.x = x;
            this.y = y;
            this.z = z;
        }

        public static Vec3i Zero { get { return new Vec3i( 0, 0, 0 ); } }

        public override string ToString( )
        {
            return $"({x},{y},{z})";
        }
    }
}

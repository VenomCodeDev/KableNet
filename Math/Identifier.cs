using System.Collections.Generic;

using KableNet.Common;

namespace KableNet.Math
{

    public class Identifier
    {
        public Identifier( string path, string value )
        {
            this.path = path;
            this.value = value;
        }

        public string path { get; }
        public string value { get; }

        public override int GetHashCode( )
        {
            return base.GetHashCode( );
        }

        public static bool operator ==( Identifier primary, Identifier secondary )
        {
            if ( primary is null && !( secondary is null ) )
            {
                return false;
            }
            if ( secondary is null && !( primary is null ) )
            {
                return false;
            }
            if ( primary is null )
            {
                // Both are null?
                return true;
            }
            return primary.Equals( secondary );
        }
        public static bool operator !=( Identifier primary, Identifier secondary )
        {
            if ( primary is null && !( secondary is null ) )
            {
                return true;
            }
            if ( secondary is null && !( primary is null ) )
            {
                return true;
            }
            if ( primary is null )
            {
                // Both are null?
                return false;
            }
            return !primary.Equals( secondary );
        }

        public override bool Equals( object obj )
        {
            if ( obj is Identifier )
            {
                Identifier other = (Identifier)obj;
                if ( other.path == path && other.value == value )
                {
                    return true;
                }
            }
            return false;
        }

        public override string ToString( )
        {
            return $"{path}:{value}";
        }

        public List<byte> ToBytes( )
        {
            KablePacket tmpPacket = new KablePacket( );

            tmpPacket.Write( path );
            tmpPacket.Write( value );

            return tmpPacket.GetRaw( );
        }
    }
}

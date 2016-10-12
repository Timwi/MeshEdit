using System;

namespace MeshEdit
{
    public struct Pt : IEquatable<Pt>
    {
        public double X; public double Y; public double Z;
        public override string ToString() { return $"({X:#.0000}, {Y:#.0000}, {Z:#.0000})"; }
        public Pt(double x, double y, double z) { X = x; Y = y; Z = z; }
        public Pt Add(double x = 0, double y = 0, double z = 0) { return new Pt(X + x, Y + y, Z + z); }
        public Pt Set(double? x = null, double? y = null, double? z = null) { return new Pt(x ?? X, y ?? Y, z ?? Z); }

        public static bool operator ==(Pt one, Pt two) => one.X == two.X && one.Y == two.Y && one.Z == two.Z;
        public static bool operator !=(Pt one, Pt two) => one.X != two.X || one.Y != two.Y || one.Z != two.Z;
        public override bool Equals(object obj) => obj is Pt && ((Pt) obj) == this;
        public override int GetHashCode() => (X.GetHashCode() * 31 + Y.GetHashCode()) * 31 + Z.GetHashCode();
        public bool Equals(Pt other) => other == this;

        public static Pt operator +(Pt one, Pt two) { return new Pt(one.X + two.X, one.Y + two.Y, one.Z + two.Z); }
        public static Pt operator *(Pt one, double two) { return new Pt(one.X * two, one.Y * two, one.Z * two); }
        public static Pt operator /(Pt one, double two) { return new Pt(one.X / two, one.Y / two, one.Z / two); }
    }
}
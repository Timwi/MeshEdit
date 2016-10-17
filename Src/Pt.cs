﻿using System;

namespace MeshEdit
{
    public struct Pt : IEquatable<Pt>
    {
        public double X; public double Y; public double Z;
        public override string ToString() { return string.Format("({0:R}, {1:R}, {2:R})", X, Y, Z); }
        public Pt(double x, double y, double z) { X = x; Y = y; Z = z; }
        public Pt Add(double x = 0, double y = 0, double z = 0) { return new Pt(X + x, Y + y, Z + z); }
        public Pt Set(double? x = null, double? y = null, double? z = null) { return new Pt(x ?? X, y ?? Y, z ?? Z); }

        public Pt RotateX(double θ) { return new Pt(X, Y * cos(θ) - Z * sin(θ), Y * sin(θ) + Z * cos(θ)); }
        public Pt RotateY(double θ) { return new Pt(X * cos(θ) - Z * sin(θ), Y, X * sin(θ) + Z * cos(θ)); }
        public Pt RotateZ(double θ) { return new Pt(X * cos(θ) - Y * sin(θ), X * sin(θ) + Y * cos(θ), Z); }

        static double sin(double θ) => Math.Sin(θ / 180 * Math.PI);
        static double cos(double θ) => Math.Cos(θ / 180 * Math.PI);

        public static bool operator ==(Pt one, Pt two) { return one.X == two.X && one.Y == two.Y && one.Z == two.Z; }
        public static bool operator !=(Pt one, Pt two) { return one.X != two.X || one.Y != two.Y || one.Z != two.Z; }
        public override bool Equals(object obj) { return obj is Pt && ((Pt) obj) == this; }
        public override int GetHashCode() { return unchecked((X.GetHashCode() * 31 + Y.GetHashCode()) * 31 + Z.GetHashCode()); }
        public bool Equals(Pt other) { return other == this; }

        public static Pt operator +(Pt one, Pt two) { return new Pt(one.X + two.X, one.Y + two.Y, one.Z + two.Z); }
        public static Pt operator -(Pt one, Pt two) { return new Pt(one.X - two.X, one.Y - two.Y, one.Z - two.Z); }
        public static Pt operator *(Pt one, double two) { return new Pt(one.X * two, one.Y * two, one.Z * two); }
        public static Pt operator *(double one, Pt two) { return new Pt(two.X * one, two.Y * one, two.Z * one); }
        public static Pt operator /(Pt one, double two) { return new Pt(one.X / two, one.Y / two, one.Z / two); }
        public static Pt operator -(Pt one) { return new Pt(-one.X, -one.Y, -one.Z); }

        // Vector cross product
        public static Pt operator *(Pt a, Pt b) { return new Pt(a.Y * b.Z - a.Z * b.Y, a.Z * b.X - a.X * b.Z, a.X * b.Y - a.Y * b.X); }

        public bool IsZero { get { return X == 0 && Y == 0 && Z == 0; } }

        public Pt Normalize()
        {
            var d = Math.Sqrt(X * X + Y * Y + Z * Z);
            if (d == 0)
                return this;
            return new Pt(X / d, Y / d, Z / d);
        }

        public Pt Rotate(Pt axisStart, Pt axisEnd, double angle)
        {
            var a = axisStart.X;
            var b = axisStart.Y;
            var c = axisStart.Z;
            var u = axisEnd.X - a;
            var v = axisEnd.Y - b;
            var w = axisEnd.Z - c;
            var nf = Math.Sqrt(u * u + v * v + w * w);
            u /= nf;
            v /= nf;
            w /= nf;
            var θ = angle * Math.PI / 180;
            var cosθ = Math.Cos(θ);
            var sinθ = Math.Sin(θ);

            return new Pt(
                (a * (v * v + w * w) - u * (b * v + c * w - u * X - v * Y - w * Z)) * (1 - cosθ) + X * cosθ + (-c * v + b * w - w * Y + v * Z) * sinθ,
                (b * (u * u + w * w) - v * (a * u + c * w - u * X - v * Y - w * Z)) * (1 - cosθ) + Y * cosθ + (c * u - a * w + w * X - u * Z) * sinθ,
                (c * (u * u + v * v) - w * (a * u + b * v - u * X - v * Y - w * Z)) * (1 - cosθ) + Z * cosθ + (-b * u + a * v - v * X + u * Y) * sinθ);
        }

        public Pt ProjectOntoPlane(Pt planeNormal)
        {
            planeNormal = planeNormal.Normalize();
            return this - Dot(planeNormal) * planeNormal;
        }

        public double Dot(Pt other)
        {
            return X * other.X + Y * other.Y + Z * other.Z;
        }

        public double Distance(Pt other)
        {
            var dx = X - other.X;
            var dy = Y - other.Y;
            var dz = Z - other.Z;
            return Math.Sqrt(dx * dx + dy * dy + dz * dz);
        }
    }
}

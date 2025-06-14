using System;

namespace testpro.Models
{
    public struct Point2D
    {
        public double X { get; set; }
        public double Y { get; set; }

        public Point2D(double x, double y)
        {
            X = x;
            Y = y;
        }

        public static double Distance(Point2D p1, Point2D p2)
        {
            double dx = p2.X - p1.X;
            double dy = p2.Y - p1.Y;
            return Math.Sqrt(dx * dx + dy * dy);
        }

        // 인스턴스 메서드로도 추가
        public double DistanceTo(Point2D other)
        {
            return Distance(this, other);
        }

        public static Point2D operator +(Point2D p1, Point2D p2)
        {
            return new Point2D(p1.X + p2.X, p1.Y + p2.Y);
        }

        public static Point2D operator -(Point2D p1, Point2D p2)
        {
            return new Point2D(p1.X - p2.X, p1.Y - p2.Y);
        }

        public static Point2D operator *(Point2D p, double scalar)
        {
            return new Point2D(p.X * scalar, p.Y * scalar);
        }

        public static Point2D operator /(Point2D p, double scalar)
        {
            return new Point2D(p.X / scalar, p.Y / scalar);
        }

        // null 비교를 위한 연산자 (구조체이지만 nullable 처리를 위해)
        public static bool operator ==(Point2D p1, Point2D p2)
        {
            return Math.Abs(p1.X - p2.X) < 0.001 && Math.Abs(p1.Y - p2.Y) < 0.001;
        }

        public static bool operator !=(Point2D p1, Point2D p2)
        {
            return !(p1 == p2);
        }

        public override string ToString()
        {
            return $"({X:F2}, {Y:F2})";
        }

        public override bool Equals(object obj)
        {
            if (obj is Point2D other)
            {
                return this == other;
            }
            return false;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(X, Y);
        }

        // 기본값
        public static readonly Point2D Zero = new Point2D(0, 0);

        // null 체크를 위한 헬퍼 (구조체는 null이 될 수 없지만 호환성을 위해)
        public bool IsValid => !double.IsNaN(X) && !double.IsNaN(Y);
    }
}
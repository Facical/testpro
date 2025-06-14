using System;

namespace testpro.Models
{
    public class Wall
    {
        public string Id { get; private set; }
        public Point2D Start { get; set; }
        public Point2D End { get; set; }
        public double Thickness { get; set; }
        public double Height { get; set; }

        // 호환성을 위한 프로퍼티 추가
        public Point2D StartPoint => Start;
        public Point2D EndPoint => End;

        public Wall(Point2D start, Point2D end, double thickness = 6.0, double height = 96.0)
        {
            Id = Guid.NewGuid().ToString();
            Start = start;
            End = end;
            Thickness = thickness; // 인치 단위 (6인치 = 약 15cm)
            Height = height; // 인치 단위 (96인치 = 8피트)
        }

        public double Length => Point2D.Distance(Start, End);

        // 실제 길이 (인치 단위)
        public double RealLengthInInches => Length;

        public Point2D GetMidpoint()
        {
            return new Point2D(
                (Start.X + End.X) / 2,
                (Start.Y + End.Y) / 2
            );
        }

        public double GetAngle()
        {
            double dx = End.X - Start.X;
            double dy = End.Y - Start.Y;
            return Math.Atan2(dy, dx) * 180 / Math.PI;
        }

        public bool IsVertical()
        {
            return Math.Abs(End.X - Start.X) < 0.1;
        }

        public bool IsHorizontal()
        {
            return Math.Abs(End.Y - Start.Y) < 0.1;
        }

        // 두 벽이 연결되어 있는지 확인
        public bool IsConnectedTo(Wall other)
        {
            if (other == null) return false;

            const double tolerance = 1.0;

            return Point2D.Distance(Start, other.Start) < tolerance ||
                   Point2D.Distance(Start, other.End) < tolerance ||
                   Point2D.Distance(End, other.Start) < tolerance ||
                   Point2D.Distance(End, other.End) < tolerance;
        }
    }
}
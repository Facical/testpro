using System;
using System.Windows.Media;

namespace testpro.Models
{
    public class Wall
    {
        public Point2D StartPoint { get; set; }
        public Point2D EndPoint { get; set; }
        public double Thickness { get; set; } = 10.0; // 기본 벽 두께 (인치)
        public Brush Stroke { get; set; } = Brushes.Black;
        public Brush Fill { get; set; } = Brushes.LightGray;
        public bool IsSelected { get; set; } = false;

        // 3D 렌더링을 위한 추가 속성
        public double Height { get; set; } = 100.0; // 벽 높이 (인치) - RFP 요구사항에 맞춤
        public string WallType { get; set; } = "Standard"; // 벽 타입
        public string Id { get; private set; } // 고유 ID

        public double? RealLengthInInches { get; set; }

        public bool IsHorizontal
        {
            get
            {
                var dx = Math.Abs(EndPoint.X - StartPoint.X);
                var dy = Math.Abs(EndPoint.Y - StartPoint.Y);
                return dx > dy;
            }
        }

        public bool IsVertical
        {
            get
            {
                return !IsHorizontal;
            }
        }

        // 실제 길이 표시를 위한 개선된 속성
        public string RealLengthDisplay
        {
            get
            {
                if (RealLengthInInches.HasValue)
                {
                    var realLength = RealLengthInInches.Value;
                    var feet = (int)(realLength / 12.0);
                    var inches = (int)(realLength % 12.0);
                    return $"{feet}'-{inches}\"";
                }
                return LengthDisplay; // 폴백
            }
        }

        public Wall(Point2D startPoint, Point2D endPoint)
        {
            StartPoint = startPoint;
            EndPoint = endPoint;
            Id = Guid.NewGuid().ToString();
        }

        public double Length
        {
            get { return StartPoint.DistanceTo(EndPoint); }
        }

        public double LengthInFeet
        {
            get { return Length / 12.0; } // Assuming 1 pixel = 1 inch
        }

        public string LengthDisplay
        {
            get
            {
                var feet = (int)LengthInFeet;
                var inches = (int)((LengthInFeet - feet) * 12);
                return $"{feet}'-{inches}\"";
            }
        }

        public Point2D MidPoint
        {
            get
            {
                return new Point2D(
                    (StartPoint.X + EndPoint.X) / 2,
                    (StartPoint.Y + EndPoint.Y) / 2
                );
            }
        }

        public bool IsConnectedTo(Wall other, double tolerance = 5.0)
        {
            return StartPoint.DistanceTo(other.StartPoint) < tolerance ||
                   StartPoint.DistanceTo(other.EndPoint) < tolerance ||
                   EndPoint.DistanceTo(other.StartPoint) < tolerance ||
                   EndPoint.DistanceTo(other.EndPoint) < tolerance;
        }

        // 벽의 방향 벡터 계산
        public Point2D GetDirection()
        {
            var dx = EndPoint.X - StartPoint.X;
            var dy = EndPoint.Y - StartPoint.Y;
            var length = Math.Sqrt(dx * dx + dy * dy);

            if (length > 0)
            {
                return new Point2D(dx / length, dy / length);
            }

            return new Point2D(0, 0);
        }

        // 벽의 각도 계산 (도 단위)
        public double GetAngleDegrees()
        {
            var direction = GetDirection();
            return Math.Atan2(direction.Y, direction.X) * 180 / Math.PI;
        }

        public override string ToString()
        {
            return $"Wall: {LengthDisplay} from ({StartPoint.X:F0}, {StartPoint.Y:F0}) to ({EndPoint.X:F0}, {EndPoint.Y:F0})";
        }
    }
}
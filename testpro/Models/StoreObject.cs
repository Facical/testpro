using System;
using System.Windows.Media;

namespace testpro.Models
{
    public enum ObjectType
    {
        Shelf,
        Refrigerator,
        Freezer,
        Checkout,
        DisplayStand,
        Pillar
    }

    public class StoreObject
    {
        public string Id { get; private set; }
        public ObjectType Type { get; set; }
        public Point2D Position { get; set; }
        public double Width { get; set; }
        public double Length { get; set; }
        public double Height { get; set; }
        public double Rotation { get; set; } = 0;
        public int Layers { get; set; }
        public bool IsHorizontal { get; set; } = true;
        public Brush Fill { get; set; }
        public Brush Stroke { get; set; }
        public bool IsSelected { get; set; }
        public string ModelPath { get; set; }

        // [추가] 투명 부품을 위한 모델 경로 속성
        public string TransparentModelPath { get; set; }

        public bool HasLayerSupport { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime ModifiedAt { get; set; }
        public string CategoryCode { get; set; }
        public double Temperature { get; set; }

        public StoreObject(ObjectType type, Point2D position)
        {
            Id = Guid.NewGuid().ToString();
            Type = type;
            Position = position;
            CreatedAt = DateTime.Now;
            ModifiedAt = DateTime.Now;
            CategoryCode = "GEN";

            switch (type)
            {
                case ObjectType.Shelf:
                    Width = 48;
                    Length = 18;
                    Height = 72;
                    Layers = 4;
                    Fill = new SolidColorBrush(Color.FromRgb(139, 69, 19));
                    HasLayerSupport = true;
                    ModelPath = @"Models\Shelf\shelf.obj";
                    break;

                case ObjectType.Refrigerator:
                    Width = 36;
                    Length = 24;
                    Height = 72;
                    Layers = 5;
                    Fill = new SolidColorBrush(Color.FromRgb(200, 220, 240));
                    HasLayerSupport = true;
                    ModelPath = @"Models\Refrigerator\beverage_refrigerator.obj";
                    // [추가] 분리된 유리 모델의 경로를 지정합니다.
                    TransparentModelPath = @"Models\Refrigerator\refrigerator_glass.obj";
                    Temperature = 4.0;
                    CategoryCode = "BEVERAGE";
                    break;

                // ... 나머지 코드는 동일 ...
                case ObjectType.Freezer:
                    Width = 36;
                    Length = 24;
                    Height = 72;
                    Layers = 3;
                    Fill = new SolidColorBrush(Color.FromRgb(150, 200, 255));
                    HasLayerSupport = true;
                    ModelPath = @"Models\Freezer\freezer.obj";
                    Temperature = -18.0;
                    break;
                case ObjectType.Checkout:
                    Width = 48;
                    Length = 36;
                    Height = 36;
                    Layers = 1;
                    Fill = new SolidColorBrush(Color.FromRgb(192, 192, 192));
                    HasLayerSupport = false;
                    ModelPath = @"Models\Checkout\checkout.obj";
                    break;
                case ObjectType.DisplayStand:
                    Width = 60;
                    Length = 30;
                    Height = 48;
                    Layers = 2;
                    Fill = new SolidColorBrush(Color.FromRgb(255, 228, 196));
                    HasLayerSupport = true;
                    ModelPath = @"Models\DisplayStand\display_stand.obj";
                    break;
                case ObjectType.Pillar:
                    Width = 12;
                    Length = 12;
                    Height = 96;
                    Layers = 1;
                    Fill = new SolidColorBrush(Color.FromRgb(128, 128, 128));
                    HasLayerSupport = false;
                    ModelPath = @"Models\Pillar\pillar.obj";
                    break;
            }

            Stroke = Brushes.Black;
        }

        // ... 나머지 메서드는 동일 ...
        public Point2D GetCenter()
        {
            return new Point2D(
                Position.X + (IsHorizontal ? Width : Length) / 2,
                Position.Y + (IsHorizontal ? Length : Width) / 2
            );
        }

        public string GetDisplayName()
        {
            switch (Type)
            {
                case ObjectType.Shelf: return "선반";
                case ObjectType.Refrigerator: return "냉장고";
                case ObjectType.Freezer: return "냉동고";
                case ObjectType.Checkout: return "계산대";
                case ObjectType.DisplayStand: return "진열대";
                case ObjectType.Pillar: return "기둥";
                default: return "객체";
            }
        }

        public StoreObject Clone()
        {
            var clone = new StoreObject(Type, new Point2D(Position.X + 20, Position.Y + 20))
            {
                Width = Width,
                Length = Length,
                Height = Height,
                Rotation = Rotation,
                Layers = Layers,
                IsHorizontal = IsHorizontal,
                CategoryCode = CategoryCode,
                Temperature = Temperature,
                Fill = Fill,
                Stroke = Stroke,
                ModelPath = ModelPath,
                TransparentModelPath = TransparentModelPath
            };
            return clone;
        }

        public bool ContainsPoint(Point2D point)
        {
            double actualWidth = IsHorizontal ? Width : Length;
            double actualLength = IsHorizontal ? Length : Width;

            return point.X >= Position.X &&
                   point.X <= Position.X + actualWidth &&
                   point.Y >= Position.Y &&
                   point.Y <= Position.Y + actualLength;
        }
    }
}
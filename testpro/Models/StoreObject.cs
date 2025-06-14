using System;
using System.Windows.Media;

namespace testpro.Models
{
    public enum ObjectType
    {
        Shelf,              // 일반 선반
        Refrigerator,       // 음료 냉장고 (오픈형)
        Freezer,            // 냉동고
        FreezerChest,       // 평면 냉동고 (아이스크림용)
        Checkout,           // 계산대
        DisplayStand,       // 진열대
        DisplayRackDouble,  // 양면 진열대
        RefrigeratorWall,   // 벽면 냉장고
        CornerDisplay,      // 코너 진열대
        Pillar              // 기둥
    }

    public class StoreObject
    {
        public string Id { get; private set; }
        public ObjectType Type { get; set; }
        public Point2D Position { get; set; }
        public double Width { get; set; }
        public double Length { get; set; }
        public double Height { get; set; }
        public double Rotation { get; set; } // 회전 각도 (도)
        public int Layers { get; set; } // 층수
        public bool IsHorizontal { get; set; } // 방향 (가로/세로)
        public Brush Fill { get; set; }
        public Brush Stroke { get; set; }
        public bool IsSelected { get; set; }

        // 3D 모델 관련 속성
        public string ModelBasePath { get; set; } // 3D 모델 기본 경로
        public string ShelfModelPath { get; set; } // 선반 모델 경로
        public bool HasLayerSupport { get; set; } // 층수 지원 여부

        // 추가 속성 (RFP 요구사항)
        public DateTime CreatedAt { get; set; }
        public DateTime ModifiedAt { get; set; }
        public string CategoryCode { get; set; } // 제품 카테고리 코드
        public double Temperature { get; set; } // 냉장고/냉동고 온도 설정

        public StoreObject(ObjectType type, Point2D position)
        {
            Id = Guid.NewGuid().ToString();
            Type = type;
            Position = position;
            IsHorizontal = true;
            CreatedAt = DateTime.Now;
            ModifiedAt = DateTime.Now;
            CategoryCode = "GEN"; // 기본값

            // 타입별 기본 설정
            switch (type)
            {
                case ObjectType.Shelf:
                    Width = 48;  // 4ft
                    Length = 18; // 1.5ft
                    Height = 72; // 6ft
                    Layers = 3;
                    Fill = new SolidColorBrush(Color.FromRgb(139, 69, 19)); // 갈색
                    HasLayerSupport = true;
                    ModelBasePath = "Models/Shelf/shelf_frame.obj";
                    ShelfModelPath = "Models/Shelf/shelf_layer.obj";
                    break;

                case ObjectType.Refrigerator:
                    Width = 36;  // 3ft
                    Length = 24; // 2ft
                    Height = 84; // 7ft
                    Layers = 2;
                    Fill = new SolidColorBrush(Color.FromRgb(200, 200, 255)); // 연한 파랑
                    HasLayerSupport = true;
                    ModelBasePath = "Models/Refrigerator/fridge_frame.obj";
                    ShelfModelPath = "Models/Refrigerator/fridge_shelf.obj";
                    Temperature = 4.0; // 섭씨 4도
                    break;

                case ObjectType.Freezer:
                    Width = 36;  // 3ft
                    Length = 24; // 2ft
                    Height = 84; // 7ft
                    Layers = 3;
                    Fill = new SolidColorBrush(Color.FromRgb(150, 200, 255)); // 더 진한 파랑
                    HasLayerSupport = true;
                    ModelBasePath = "Models/Freezer/freezer_frame.obj";
                    ShelfModelPath = "Models/Freezer/freezer_shelf.obj";
                    Temperature = -18.0; // 섭씨 -18도
                    break;

                case ObjectType.Checkout:
                    Width = 48;  // 4ft
                    Length = 36; // 3ft
                    Height = 36; // 3ft
                    Layers = 1;
                    Fill = new SolidColorBrush(Color.FromRgb(192, 192, 192)); // 은색
                    HasLayerSupport = false;
                    ModelBasePath = "Models/Checkout/checkout.obj";
                    break;

                case ObjectType.DisplayStand:
                    Width = 60;  // 5ft
                    Length = 30; // 2.5ft
                    Height = 48; // 4ft
                    Layers = 2;
                    Fill = new SolidColorBrush(Color.FromRgb(255, 228, 196)); // 베이지
                    HasLayerSupport = true;
                    ModelBasePath = "Models/DisplayStand/display_frame.obj";
                    ShelfModelPath = "Models/DisplayStand/display_shelf.obj";
                    break;

                case ObjectType.Pillar:
                    Width = 12;  // 1ft
                    Length = 12; // 1ft
                    Height = 96; // 8ft
                    Layers = 1;
                    Fill = new SolidColorBrush(Color.FromRgb(128, 128, 128)); // 회색
                    HasLayerSupport = false;
                    ModelBasePath = "Models/Pillar/pillar.obj";
                    break;

                case ObjectType.FreezerChest:
                    Width = 72;  // 6ft
                    Length = 36; // 3ft  
                    Height = 36; // 3ft (낮은 높이)
                    Layers = 1;
                    Fill = new SolidColorBrush(Color.FromRgb(180, 200, 220));
                    HasLayerSupport = false;
                    ModelBasePath = "Models/FreezerChest/freezer_chest.gltf";
                    Temperature = -18.0;
                    break;

                case ObjectType.DisplayRackDouble:
                    Width = 48;  // 4ft
                    Length = 36; // 3ft (양면이므로 더 깊음)
                    Height = 72; // 6ft
                    Layers = 5;  // 보통 5층
                    Fill = new SolidColorBrush(Color.FromRgb(50, 50, 50)); // 검은색 프레임
                    HasLayerSupport = true;
                    ModelBasePath = "Models/DisplayRack/display_rack_double.gltf";
                    ShelfModelPath = "Models/DisplayRack/shelf_yellow.gltf"; // 노란색 선반
                    break;

                case ObjectType.RefrigeratorWall:
                    Width = 96;  // 8ft (긴 벽면형)
                    Length = 24; // 2ft
                    Height = 84; // 7ft
                    Layers = 6;  // 6층
                    Fill = new SolidColorBrush(Color.FromRgb(30, 30, 30)); // 검은색
                    HasLayerSupport = true;
                    ModelBasePath = "Models/Refrigerator/refrigerator_wall.gltf";
                    Temperature = 4.0;
                    break;
            }

            Stroke = Brushes.Black;
        }

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

        // 객체의 경계 상자 가져오기
        public (Point2D min, Point2D max) GetBoundingBox()
        {
            double actualWidth = IsHorizontal ? Width : Length;
            double actualLength = IsHorizontal ? Length : Width;

            return (
                new Point2D(Position.X, Position.Y),
                new Point2D(Position.X + actualWidth, Position.Y + actualLength)
            );
        }

        // 점이 객체 내부에 있는지 확인
        public bool ContainsPoint(Point2D point)
        {
            var (min, max) = GetBoundingBox();
            return point.X >= min.X && point.X <= max.X &&
                   point.Y >= min.Y && point.Y <= max.Y;
        }

        // 객체 이동
        public void MoveTo(Point2D newPosition)
        {
            Position = newPosition;
            ModifiedAt = DateTime.Now;
        }

        // 객체 회전 (90도 단위)
        public void Rotate()
        {
            IsHorizontal = !IsHorizontal;
            Rotation = IsHorizontal ? 0 : 90;
            ModifiedAt = DateTime.Now;
        }

        // 층별 높이 계산
        public double GetLayerHeight()
        {
            if (Layers <= 0) return Height;
            return Height / Layers;
        }

        // 특정 층의 Z 위치 계산 (3D용)
        public double GetLayerZPosition(int layerIndex)
        {
            if (layerIndex < 0 || layerIndex >= Layers) return 0;

            double layerHeight = GetLayerHeight();

            // 바닥부터 시작하여 각 층의 위치 계산
            return layerIndex * layerHeight;
        }

        // 객체 정보 업데이트
        public void UpdateProperties(double height, int layers, bool isHorizontal)
        {
            Height = height;
            Layers = layers;
            IsHorizontal = isHorizontal;
            Rotation = isHorizontal ? 0 : 90;
            ModifiedAt = DateTime.Now;
        }

        public override string ToString()
        {
            return $"{GetDisplayName()} - 위치: ({Position.X:F0}, {Position.Y:F0}), " +
                   $"크기: {Width:F0}x{Length:F0}x{Height:F0}, 층수: {Layers}, " +
                   $"방향: {(IsHorizontal ? "가로" : "세로")}";
        }
    }
}
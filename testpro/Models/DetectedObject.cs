using System.Windows;

namespace testpro.Models
{
    public enum DetectedObjectType
    {
        Unknown,
        Shelf,
        DisplayRackDouble,     // 추가
        Refrigerator,
        RefrigeratorWall,      // 추가
        Freezer,
        FreezerChest,          // 추가
        Checkout,
        DisplayStand,
        Pillar,
        Table,
        Chair,
        Door,
        Window,
        Desk,
        Microwave
    }

    public class DetectedObject
    {
        public DetectedObjectType Type { get; set; }
        public Rect Bounds { get; set; }
        public double Confidence { get; set; }

        public string GetTypeName()
        {
            switch (Type)
            {
                case DetectedObjectType.Shelf: return "선반";
                case DetectedObjectType.DisplayRackDouble: return "양면진열대";
                case DetectedObjectType.Refrigerator: return "냉장고";
                case DetectedObjectType.RefrigeratorWall: return "벽면냉장고";
                case DetectedObjectType.Freezer: return "냉동고";
                case DetectedObjectType.FreezerChest: return "평형냉동고";
                case DetectedObjectType.Checkout: return "계산대";
                case DetectedObjectType.DisplayStand: return "진열대";
                case DetectedObjectType.Pillar: return "기둥";
                case DetectedObjectType.Table: return "테이블";
                case DetectedObjectType.Chair: return "의자";
                case DetectedObjectType.Door: return "문";
                case DetectedObjectType.Window: return "창문";
                case DetectedObjectType.Desk: return "책상";
                case DetectedObjectType.Microwave: return "전자레인지";
                default: return "미지정";
            }
        }

        public StoreObject ToStoreObjectWithProperties(
            double width, double height, double length,
            int layers, bool isHorizontal,
            double temperature = 0, string categoryCode = "GEN")
        {
            ObjectType storeType = ObjectType.Shelf; // 기본값

            switch (Type)
            {
                case DetectedObjectType.Shelf:
                case DetectedObjectType.DisplayRackDouble:
                    storeType = ObjectType.Shelf;
                    break;
                case DetectedObjectType.Refrigerator:
                case DetectedObjectType.RefrigeratorWall:
                    storeType = ObjectType.Refrigerator;
                    break;
                case DetectedObjectType.Freezer:
                case DetectedObjectType.FreezerChest:
                    storeType = ObjectType.Freezer;
                    break;
                case DetectedObjectType.Checkout:
                    storeType = ObjectType.Checkout;
                    break;
                case DetectedObjectType.DisplayStand:
                    storeType = ObjectType.DisplayStand;
                    break;
                case DetectedObjectType.Pillar:
                    storeType = ObjectType.Pillar;
                    break;
            }

            var position = new Point2D(Bounds.Left, Bounds.Top);
            var obj = new StoreObject(storeType, position)
            {
                Width = width,
                Length = length,
                Height = height,
                Layers = layers,
                IsHorizontal = isHorizontal,
                CategoryCode = categoryCode
            };

            // 온도 설정 (냉장고/냉동고)
            if (storeType == ObjectType.Refrigerator || storeType == ObjectType.Freezer)
            {
                obj.Temperature = temperature;
            }

            return obj;
        }

        public StoreObject ToStoreObject()
        {
            return ToStoreObjectWithProperties(
                Bounds.Width,
                72,  // 기본 높이
                Bounds.Height,
                3,   // 기본 층수
                true, // 기본 가로방향
                4.0,  // 기본 온도
                "GEN" // 기본 카테고리
            );
        }
    }
}
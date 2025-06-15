using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using testpro.Models;

namespace testpro.Services
{
    #region Undo/Redo Command Pattern
    public interface ICommandAction
    {
        void Execute();
        void Unexecute();
    }

    public class AddObjectCommand : ICommandAction
    {
        private readonly DrawingService _service;
        private readonly StoreObject _object;

        public AddObjectCommand(DrawingService service, StoreObject obj)
        {
            _service = service;
            _object = obj;
        }

        public void Execute() => _service.StoreObjects.Add(_object);
        public void Unexecute() => _service.StoreObjects.Remove(_object);
    }

    public class RemoveObjectCommand : ICommandAction
    {
        private readonly DrawingService _service;
        private readonly StoreObject _object;
        private readonly int _index;

        public RemoveObjectCommand(DrawingService service, StoreObject obj)
        {
            _service = service;
            _object = obj;
            _index = service.StoreObjects.IndexOf(obj);
        }

        public void Execute() => _service.StoreObjects.Remove(_object);
        public void Unexecute()
        {
            if (_index >= 0 && _index <= _service.StoreObjects.Count)
            {
                _service.StoreObjects.Insert(_index, _object);
            }
            else
            {
                _service.StoreObjects.Add(_object);
            }
        }
    }

    public class MoveObjectCommand : ICommandAction
    {
        private readonly StoreObject _object;
        private readonly Point2D _from;
        private readonly Point2D _to;

        public MoveObjectCommand(StoreObject obj, Point2D from, Point2D to)
        {
            _object = obj;
            _from = from;
            _to = to;
        }

        public void Execute() => _object.Position = _to;
        public void Unexecute() => _object.Position = _from;
    }

    public class UpdateObjectPropertiesCommand : ICommandAction
    {
        private readonly StoreObject _object;
        private readonly StoreObject _oldState;
        private readonly StoreObject _newState;

        public UpdateObjectPropertiesCommand(StoreObject target, StoreObject newState)
        {
            _object = target;
            _oldState = target.Clone(); // 이전 상태 저장
            _newState = newState;
        }

        public void Execute() => _object.ApplyState(_newState);
        public void Unexecute() => _object.ApplyState(_oldState);
    }
    #endregion

    public class DrawingService : INotifyPropertyChanged
    {
        public List<Wall> Walls { get; } = new List<Wall>();
        public List<Room> Rooms { get; } = new List<Room>();
        public List<StoreObject> StoreObjects { get; } = new List<StoreObject>();

        private readonly Stack<ICommandAction> _undoStack = new Stack<ICommandAction>();
        private readonly Stack<ICommandAction> _redoStack = new Stack<ICommandAction>();

        private const double SnapDistance = 10.0;

        private double _scaleX = 1.0;
        private double _scaleY = 1.0;

        public double ScaleX
        {
            get => _scaleX;
            private set { _scaleX = value > 0 ? value : 1.0; OnPropertyChanged(); }
        }

        public double ScaleY
        {
            get => _scaleY;
            private set { _scaleY = value > 0 ? value : 1.0; OnPropertyChanged(); }
        }

        public double Scale
        {
            get => (_scaleX + _scaleY) / 2.0;
            private set { ScaleX = value; ScaleY = value; }
        }

        public string BackgroundImagePath { get; set; }

        public event PropertyChangedEventHandler PropertyChanged;

        private void ExecuteCommand(ICommandAction command)
        {
            command.Execute();
            _undoStack.Push(command);
            _redoStack.Clear(); // 새로운 작업이 실행되면 Redo 스택은 비워짐
            NotifyChanged();
        }

        public void Undo()
        {
            if (_undoStack.Count > 0)
            {
                var command = _undoStack.Pop();
                command.Unexecute();
                _redoStack.Push(command);
                NotifyChanged();
            }
        }

        public void Redo()
        {
            if (_redoStack.Count > 0)
            {
                var command = _redoStack.Pop();
                command.Execute();
                _undoStack.Push(command);
                NotifyChanged();
            }
        }

        public void SetScale(double scale) => Scale = scale;
        public void SetScaleXY(double scaleX, double scaleY) { ScaleX = scaleX; ScaleY = scaleY; }

        public Point2D SnapToExistingPoint(Point2D point)
        {
            foreach (var wall in Walls)
            {
                if (point.DistanceTo(wall.StartPoint) < SnapDistance) return wall.StartPoint;
                if (point.DistanceTo(wall.EndPoint) < SnapDistance) return wall.EndPoint;
            }
            return point;
        }

        public Wall AddWall(Point2D startPoint, Point2D endPoint)
        {
            var wall = new Wall(SnapToExistingPoint(startPoint), SnapToExistingPoint(endPoint));
            Walls.Add(wall);
            UpdateRooms();
            NotifyChanged();
            return wall;
        }

        public StoreObject AddStoreObject(ObjectType type, Point2D position, double width, double height)
        {
            var obj = new StoreObject(type, position) { Width = width, Length = height };
            var command = new AddObjectCommand(this, obj);
            ExecuteCommand(command);
            return obj;
        }

        public StoreObject AddStoreObject(ObjectType type, Point2D position, double width, double length, double height, int layers, bool isHorizontal, double temperature, string categoryCode)
        {
            var obj = new StoreObject(type, position)
            {
                Width = width,
                Length = length,
                Height = height,
                Layers = layers,
                IsHorizontal = isHorizontal,
                Temperature = temperature,
                CategoryCode = categoryCode
            };
            var command = new AddObjectCommand(this, obj);
            ExecuteCommand(command);
            return obj;
        }


        public void RemoveStoreObject(StoreObject obj)
        {
            if (obj == null) return;
            var command = new RemoveObjectCommand(this, obj);
            ExecuteCommand(command);
        }

        public void MoveStoreObject(StoreObject obj, Point2D from, Point2D to)
        {
            var command = new MoveObjectCommand(obj, from, to);
            ExecuteCommand(command);
        }

        public void UpdateStoreObject(StoreObject obj, double width, double length, double height, int layers, bool isHorizontal, double temperature, string categoryCode)
        {
            var newState = obj.Clone();
            newState.Width = width;
            newState.Length = length;
            newState.Height = height;
            newState.Layers = layers;
            newState.IsHorizontal = isHorizontal;
            newState.Temperature = temperature;
            newState.CategoryCode = categoryCode;
            newState.Rotation = isHorizontal ? 0 : 90;
            newState.ModifiedAt = DateTime.Now;

            var command = new UpdateObjectPropertiesCommand(obj, newState);
            ExecuteCommand(command);
        }

        public StoreObject GetObjectAt(Point2D point)
        {
            return StoreObjects.LastOrDefault(obj => obj.ContainsPoint(point));
        }

        public void Clear()
        {
            Walls.Clear();
            Rooms.Clear();
            StoreObjects.Clear();
            _undoStack.Clear();
            _redoStack.Clear();
            BackgroundImagePath = null;
            ScaleX = 1.0;
            ScaleY = 1.0;
            NotifyChanged();
        }

        private void UpdateRooms()
        {
            Rooms.Clear();
            var processedWalls = new HashSet<Wall>();
            foreach (var wall in Walls)
            {
                if (processedWalls.Contains(wall)) continue;
                var room = TryCreateRoomFromWall(wall, processedWalls);
                if (room != null && room.IsClosedRoom() && room.Area > 100)
                {
                    Rooms.Add(room);
                }
            }
        }

        private Room TryCreateRoomFromWall(Wall startWall, HashSet<Wall> processedWalls)
        {
            var room = new Room();
            var currentWalls = new List<Wall>();
            var visited = new HashSet<Wall>();
            if (FindConnectedWalls(startWall, currentWalls, visited))
            {
                foreach (var wall in currentWalls)
                {
                    room.AddWall(wall);
                    processedWalls.Add(wall);
                }
                return room;
            }
            return null;
        }

        private bool FindConnectedWalls(Wall startWall, List<Wall> roomWalls, HashSet<Wall> visited)
        {
            if (visited.Contains(startWall)) return false;
            visited.Add(startWall);
            roomWalls.Add(startWall);

            foreach (var connected in GetConnectedWalls(startWall))
            {
                if (!visited.Contains(connected))
                {
                    if (FindConnectedWalls(connected, roomWalls, visited)) return true;
                }
                else if (connected == roomWalls.First() && roomWalls.Count >= 3)
                {
                    return true;
                }
            }
            return false;
        }

        private List<Wall> GetConnectedWalls(Wall wall)
        {
            return Walls.Where(other => other != wall && wall.IsConnectedTo(other)).ToList();
        }

        private void NotifyChanged([CallerMemberName] string propertyName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
using System;
using System.Collections.Generic;
using LiteNetLib;

namespace PA.Server.Room {
    public class MapEvent {
        public delegate void EventDelegate();

        private struct Event {
            public uint Timer;
            public uint Duration;
            public EventDelegate Delegate;

            public Event(uint timer, uint duration, EventDelegate del) {
                Timer = timer;
                Duration = duration;
                Delegate = del;
            }
        }

        private struct RadiusDescription {
            public float StartRadius;
            public float EndRadius;
            public uint Duration;

            public RadiusDescription(float startRadius, float endRadius, uint duration) {
                StartRadius = startRadius;
                EndRadius = endRadius;
                Duration = duration;
            }
        }

        public Dictionary<int, ItemGenerator.Item> ItemList;
        public Dictionary<int, EnigmasGenerator.Enigmas> EnigmasList;
        public MyNetworkServer<Player.InGamePlayer> Server;

        private static readonly Dictionary<int, RadiusDescription> RadiusZone = new Dictionary<int, RadiusDescription> {
            {1, new RadiusDescription(500, 300, 60)},
            {2, new RadiusDescription(300, 200, 60)}
        };

        private List<Event> _events;

        private int _currentZoneIndex;
        private float _currentZoneRadius;
        private float _currentTrainPos;
        private DateTime _start;

        public MapEvent() {
            _events = new List<Event> {
                new Event(0, 0, StartTrain),
                new Event(0, 60, MoveTrain),
                new Event(60, 0, StopTrain),
                new Event(120, 0, OpenAccessZone),
                new Event(300, 0, StartingPlasmaZone),
                new Event(300, RadiusZone[1].Duration, MovingPlasmaZone),
                new Event(360, 0, OpenAccessZone),
                new Event(540, 0, StartingPlasmaZone),
                new Event(540, RadiusZone[2].Duration, MovingPlasmaZone)
            };

            _currentZoneIndex = 1;
            _currentZoneRadius = RadiusZone[1].StartRadius;

            _currentTrainPos = 0;
            
            _start = DateTime.Now;
        }

        /// <summary>
        /// Call each second
        /// </summary>
        public void Update() {
            var diff = DateTime.Now - _start;
            uint time = (uint)diff.Seconds;

            foreach (var e in _events) {
                if (time >= e.Timer && time <= e.Timer + e.Duration)
                    e.Delegate();
            }
        }
        
        private void StartTrain() {
            var writer = new NetworkWriter(InGameProtocol.TCPServerToClient.StartTrain);
            Server.SendAll(writer, DeliveryMethod.ReliableOrdered);
        }

        private void MoveTrain() {
            _currentTrainPos += 0.1f;
            
            var writer = new NetworkWriter(InGameProtocol.UDPServerToClient.MoveTrain);
            writer.Put(_currentTrainPos);
            Server.SendAll(writer, DeliveryMethod.Unreliable);
        }

        private void StopTrain() {
            var writer = new NetworkWriter(InGameProtocol.TCPServerToClient.StopTrain);
            Server.SendAll(writer, DeliveryMethod.ReliableOrdered);
        }

        private void OpenAccessZone() {
            foreach (var e in EnigmasList) {
                var enigma = EnigmasList[e.Key];
                if (enigma.Zone == _currentZoneIndex)
                    enigma.EnigmaAccessOpened = true;
            }

            var writer = new NetworkWriter(InGameProtocol.TCPServerToClient.EnigmaAccessOpened);
            writer.Put(_currentZoneIndex + 1);
            Server.SendAll(writer, DeliveryMethod.ReliableOrdered);
        }

        private void StartingPlasmaZone() {
            var writer = new NetworkWriter(InGameProtocol.TCPServerToClient.StartPlasma);
            writer.Put(_currentZoneIndex);
            Server.SendAll(writer, DeliveryMethod.ReliableOrdered);

            _currentZoneIndex++;
        }

        private void MovingPlasmaZone() {
            var zone = RadiusZone[_currentZoneIndex - 1];
            float step = (zone.StartRadius - zone.EndRadius) / zone.Duration;

            _currentZoneRadius -= step;
            
            var writer = new NetworkWriter(InGameProtocol.TCPServerToClient.MovingPlasma);
            writer.Put(_currentZoneIndex - 1);
            writer.Put(_currentZoneRadius);
            Server.SendAll(writer, DeliveryMethod.ReliableOrdered);
        }
    }
}
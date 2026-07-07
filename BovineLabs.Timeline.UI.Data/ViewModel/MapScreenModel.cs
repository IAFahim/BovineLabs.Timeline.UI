using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using BovineLabs.Anchor;

namespace BovineLabs.Timeline.UI.Data.ViewModel
{
    /// <summary>
    /// Screen model for the node-graph map / room-select nav screen. Unlike the HUD readout view models (which are
    /// system-fed <c>SystemObservableObject</c>s pushed from ECS), a map screen is UI-authoritative — the player picks a
    /// room — so it is a plain <see cref="INotifyPropertyChanged"/> model App UI can data-bind to. Populate <see cref="Rooms"/>
    /// from wherever the run's map lives (a MapBlob, a save, a generator) via <see cref="SetRooms"/>; the map is shared
    /// across all players (party map), so it carries no player index. The MapScreen view renders one <c>NodeChip</c> per
    /// room and calls <see cref="Select"/> on a legal pick.
    /// </summary>
    [IsService]
    public sealed class MapScreenModel : INotifyPropertyChanged
    {
        private readonly List<Room> rooms = new();
        private int selectedId = -1;

        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>The rooms of the current map segment. Rebuilt via <see cref="SetRooms"/>.</summary>
        public IReadOnlyList<Room> Rooms => this.rooms;

        /// <summary>The id of the currently selected room, or -1 when none is chosen.</summary>
        public int SelectedId
        {
            get => this.selectedId;
            private set
            {
                if (this.selectedId == value)
                {
                    return;
                }

                this.selectedId = value;
                this.OnPropertyChanged();
            }
        }

        /// <summary>Replaces the map's rooms and clears the current selection.</summary>
        public void SetRooms(IEnumerable<Room> newRooms)
        {
            this.rooms.Clear();
            if (newRooms != null)
            {
                this.rooms.AddRange(newRooms);
            }

            this.selectedId = -1;
            this.OnPropertyChanged(nameof(this.Rooms));
            this.OnPropertyChanged(nameof(this.SelectedId));
        }

        /// <summary>Selects a legal room by id; ignores unknown or illegal rooms.</summary>
        public bool Select(int id)
        {
            for (var i = 0; i < this.rooms.Count; i++)
            {
                if (this.rooms[i].Id == id && this.rooms[i].IsLegal)
                {
                    this.SelectedId = id;
                    return true;
                }
            }

            return false;
        }

        private void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        /// <summary>One node in the map graph. Position is normalized (0..1) within the map area.</summary>
        public struct Room
        {
            public int Id;
            public string Kind;   // room kind key → NodeChip.kind USS palette (combat/elite/shop/rest/boss…)
            public string Glyph;  // short label shown in the chip
            public float X;
            public float Y;
            public bool IsLegal;  // reachable from the party's current position this turn
            public bool IsCurrent; // the party's current room
        }
    }
}

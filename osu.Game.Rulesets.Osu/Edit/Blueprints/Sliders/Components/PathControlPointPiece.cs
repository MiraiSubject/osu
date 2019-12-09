// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Lines;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Input.Events;
using osu.Game.Graphics;
using osu.Game.Rulesets.Edit;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Objects.Types;
using osu.Game.Rulesets.Osu.Objects;
using osuTK;
using osuTK.Graphics;
using osuTK.Input;

namespace osu.Game.Rulesets.Osu.Edit.Blueprints.Sliders.Components
{
    public class PathControlPointPiece : BlueprintPiece<Slider>
    {
        public Action<PathControlPointPiece, MouseButtonEvent> RequestSelection;

        public readonly BindableBool IsSelected = new BindableBool();

        public readonly PathControlPoint ControlPoint;

        private readonly Slider slider;
        private readonly Path path;
        private readonly Container marker;
        private readonly Drawable markerRing;

        [Resolved(CanBeNull = true)]
        private IDistanceSnapProvider snapProvider { get; set; }

        [Resolved]
        private OsuColour colours { get; set; }

        private IBindable<Vector2> sliderPosition;
        private IBindable<int> pathVersion;

        public PathControlPointPiece(Slider slider, PathControlPoint controlPoint)
        {
            this.slider = slider;

            ControlPoint = controlPoint;

            Origin = Anchor.Centre;
            AutoSizeAxes = Axes.Both;

            InternalChildren = new Drawable[]
            {
                path = new SmoothPath
                {
                    Anchor = Anchor.Centre,
                    PathRadius = 1
                },
                marker = new Container
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    AutoSizeAxes = Axes.Both,
                    Children = new[]
                    {
                        new Circle
                        {
                            Anchor = Anchor.Centre,
                            Origin = Anchor.Centre,
                            Size = new Vector2(10),
                        },
                        markerRing = new CircularContainer
                        {
                            Anchor = Anchor.Centre,
                            Origin = Anchor.Centre,
                            Size = new Vector2(14),
                            Masking = true,
                            BorderThickness = 2,
                            BorderColour = Color4.White,
                            Alpha = 0,
                            Child = new Box
                            {
                                RelativeSizeAxes = Axes.Both,
                                Alpha = 0,
                                AlwaysPresent = true
                            }
                        }
                    }
                }
            };
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            sliderPosition = slider.PositionBindable.GetBoundCopy();
            sliderPosition.BindValueChanged(_ => updateDisplay());

            pathVersion = slider.Path.Version.GetBoundCopy();
            pathVersion.BindValueChanged(_ => updateDisplay());

            IsSelected.BindValueChanged(_ => updateMarkerDisplay());

            updateDisplay();
        }

        private void updateDisplay()
        {
            updateMarkerDisplay();
            updateConnectingPath();
        }

        // The connecting path is excluded from positional input
        public override bool ReceivePositionalInputAt(Vector2 screenSpacePos) => marker.ReceivePositionalInputAt(screenSpacePos);

        protected override bool OnHover(HoverEvent e)
        {
            updateMarkerDisplay();
            return true;
        }

        protected override void OnHoverLost(HoverLostEvent e)
        {
            updateMarkerDisplay();
        }

        protected override bool OnMouseDown(MouseDownEvent e)
        {
            if (RequestSelection == null)
                return false;

            switch (e.Button)
            {
                case MouseButton.Left:
                    RequestSelection.Invoke(this, e);
                    return true;

                case MouseButton.Right:
                    if (!IsSelected.Value)
                        RequestSelection.Invoke(this, e);
                    return false; // Allow context menu to show
            }

            return false;
        }

        protected override bool OnMouseUp(MouseUpEvent e) => RequestSelection != null;

        protected override bool OnClick(ClickEvent e) => RequestSelection != null;

        protected override bool OnDragStart(DragStartEvent e) => e.Button == MouseButton.Left;

        protected override bool OnDrag(DragEvent e)
        {
            if (ControlPoint == slider.Path.ControlPoints[0])
            {
                // Special handling for the head control point - the position of the slider changes which means the snapped position and time have to be taken into account
                (Vector2 snappedPosition, double snappedTime) = snapProvider?.GetSnappedPosition(e.MousePosition, slider.StartTime) ?? (e.MousePosition, slider.StartTime);
                Vector2 movementDelta = snappedPosition - slider.Position;

                slider.Position += movementDelta;
                slider.StartTime = snappedTime;

                // Since control points are relative to the position of the slider, they all need to be offset backwards by the delta
                for (int i = 1; i < slider.Path.ControlPoints.Count; i++)
                    slider.Path.ControlPoints[i].Position.Value -= movementDelta;
            }
            else
                ControlPoint.Position.Value += e.Delta;

            return true;
        }

        protected override bool OnDragEnd(DragEndEvent e) => true;

        /// <summary>
        /// Updates the state of the circular control point marker.
        /// </summary>
        private void updateMarkerDisplay()
        {
            Position = slider.StackedPosition + ControlPoint.Position.Value;

            markerRing.Alpha = IsSelected.Value ? 1 : 0;

            Color4 colour = ControlPoint.Type.Value != null ? colours.Red : colours.Yellow;
            if (IsHovered || IsSelected.Value)
                colour = Color4.White;
            marker.Colour = colour;
        }

        /// <summary>
        /// Updates the path connecting this control point to the previous one.
        /// </summary>
        private void updateConnectingPath()
        {
            path.ClearVertices();

            int index = slider.Path.ControlPoints.IndexOf(ControlPoint);

            if (index == -1)
                return;

            if (++index != slider.Path.ControlPoints.Count)
            {
                path.AddVertex(Vector2.Zero);
                path.AddVertex(slider.Path.ControlPoints[index].Position.Value - ControlPoint.Position.Value);
            }

            path.OriginPosition = path.PositionInBoundingBox(Vector2.Zero);
        }
    }
}

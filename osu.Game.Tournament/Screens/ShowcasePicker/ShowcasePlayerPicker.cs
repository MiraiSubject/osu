// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Threading;
using System.Collections.Specialized;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Game.Database;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.UserInterface;
using osu.Game.Online.API;
using osu.Game.Online.Spectator;
using osu.Game.Overlays.Dashboard;
using osu.Game.Users;

namespace osu.Game.Tournament.Screens.ShowcasePlayerPicker
{
    public class ShowcasePlayerPicker : TournamentScreen
    {
        private LoadingLayer loading;
        private OsuScrollContainer scrollFlow;

        private readonly IBindable<APIState> apiState = new Bindable<APIState>();

        private readonly IBindableList<int> playingUsers = new BindableList<int>();

        [Resolved]
        private SpectatorStreamingClient spectatorStreaming { get; set; }

        [Resolved]
        private UserLookupCache users { get; set; }

        [Resolved]
        protected IAPIProvider API { get; private set; }

        private FillFlowContainer<PlayerRow> userFlow;
        private OsuButton loadButton;

        [BackgroundDependencyLoader]
        private void load(IAPIProvider api)
        {
            apiState.BindTo(api.State);
            RelativeSizeAxes = Axes.Both;
            InternalChildren = new Drawable[]
            {
                new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = Colour4.DarkTurquoise
                },
                scrollFlow = new OsuScrollContainer
                {
                    RelativeSizeAxes = Axes.Both,
                    ScrollbarVisible = false,
                    Child = new FillFlowContainer
                    {
                        AutoSizeAxes = Axes.Y,
                        RelativeSizeAxes = Axes.X,
                        Direction = FillDirection.Vertical,
                        Children = new Drawable[]
                        {
                            userFlow = new FillFlowContainer<PlayerRow>
                            {
                                Direction = FillDirection.Vertical,
                                RelativeSizeAxes = Axes.X,
                                AutoSizeAxes = Axes.Y,
                                Spacing = new osuTK.Vector2(20)
                            }
                        }
                    }
                },
                loading = new LoadingLayer(userFlow),
                loadButton = new OsuButton
                {
                    Origin = Anchor.Centre,
                    Anchor = Anchor.Centre,
                    Size = new osuTK.Vector2(100),
                    Text = "Load",
                    Action = () =>
                    {
                        if (API.IsLoggedIn)
                        {
                            playingUsers.BindTo(spectatorStreaming.PlayingUsers);
                            playingUsers.BindCollectionChanged(onUsersChanged, true);
                            scrollFlow.ScrollToStart();
                            loading.Hide();
                            loadButton.Hide();
                        }
                    }
                },
            };
        }

        private void onUsersChanged(object sender, NotifyCollectionChangedEventArgs e) => Schedule(() =>
        {
            switch (e.Action)
            {
                case NotifyCollectionChangedAction.Add:
                    foreach (var id in e.NewItems.OfType<int>().ToArray())
                    {
                        users.GetUserAsync(id).ContinueWith(u =>
                        {
                            if (u.Result == null) return;

                            Schedule(() =>
                            {
                                // user may no longer be playing.
                                if (!playingUsers.Contains(u.Result.Id))
                                    return;

                                userFlow.Add(createUserPanel(u.Result));
                            });
                        });
                    }

                    break;

                case NotifyCollectionChangedAction.Remove:
                    foreach (var u in e.OldItems.OfType<int>())
                        userFlow.FirstOrDefault(card => card.User.Id == u)?.Expire();
                    break;

                case NotifyCollectionChangedAction.Reset:
                    userFlow.Clear();
                    break;
            }
        });

        private PlayerRow createUserPanel(User user) =>
    new PlayerRow(user).With(panel =>
    {
        panel.Anchor = Anchor.TopCentre;
        panel.Origin = Anchor.TopCentre;
    });

        protected override void LoadComplete()
        {
            base.LoadComplete();

            loading.Show();
        }

        public class PlayerRow : CompositeDrawable
        {
            public readonly User User;

            [Resolved(canBeNull: true)]
            private TournamentSceneManager sceneManager { get; set; }

            public PlayerRow(User user)
            {
                User = user;

                AutoSizeAxes = Axes.Both;
            }

            [BackgroundDependencyLoader]
            private void load(IAPIProvider api)
            {
                InternalChildren = new Drawable[]
                {
                    new FillFlowContainer
                    {
                        AutoSizeAxes = Axes.Y,
                        Direction = FillDirection.Vertical,
                        Spacing = new osuTK.Vector2(2),
                        Width = 290,
                        Children = new Drawable[]
                        {
                            new UserGridPanel(User)
                            {
                                RelativeSizeAxes = Axes.X,
                                Anchor = Anchor.TopCentre,
                                Origin = Anchor.TopCentre,
                            },
                            new TourneyButton
                            {
                                RelativeSizeAxes = Axes.X,
                                Text = "Watch",
                                Anchor = Anchor.TopCentre,
                                Origin = Anchor.TopCentre,
                                Action = () => sceneManager.User.Value = User,
                                Enabled = { Value = User.Id != api.LocalUser.Value.Id }
                            }
                        }
                    },
                };
            }
        }
    }
}

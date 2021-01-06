// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Screens;
using osu.Game.Tournament.Components;
using osu.Framework.Graphics.Shapes;
using osu.Game.Users;
using osu.Game.Screens;
using osu.Game.Screens.Play;
using osuTK.Graphics;

namespace osu.Game.Tournament.Screens.Showcase
{
    [Cached]
    public class ShowcaseScreen : BeatmapInfoScreen // IProvideVideo
    {
        private Container container;

        [Resolved(canBeNull: true)]
        private TournamentSceneManager sceneManager { get; set; }
        public Bindable<User> player = new Bindable<User>();
        public ScreenStack stack;

        public ShowcaseScreen(User player = null)
        {
            if(player != null)
                this.player.Value = player;
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            if (sceneManager != null)
                player = sceneManager.User;

            AddRangeInternal(new Drawable[]
            {
                new TournamentLogo(),
                new TourneyVideo("showcase")
                {
                    Loop = true,
                    RelativeSizeAxes = Axes.Both,
                },
                container = new DrawSizePreservingFillContainer
                {
                    Padding = new MarginPadding { Bottom = SongBar.HEIGHT },
                    RelativeSizeAxes = Axes.Both,
                    Child = new Box
                    {
                        // chroma key area for stable gameplay
                        Name = "chroma",
                        Anchor = Anchor.TopCentre,
                        Origin = Anchor.TopCentre,
                        RelativeSizeAxes = Axes.Both,
                        Colour = new Color4(0, 255, 0, 255),
                    }
                }
            });

            player.BindValueChanged(v =>
            {
                if (v.NewValue != null)
                {
                    container.Child = stack = new OsuScreenStack
                    {
                        Anchor = Anchor.TopCentre,
                        Origin = Anchor.TopCentre,
                    };
                    stack.Push(new Spectator(v.NewValue));
                }
            }, true);
        }
    }
}

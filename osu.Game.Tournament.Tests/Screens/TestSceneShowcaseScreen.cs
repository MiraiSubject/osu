// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Screens;
using osu.Framework.Testing;
using osu.Game.Beatmaps;
using osu.Game.Tournament.Screens.Showcase;
using osu.Game.Screens.Play;
using osu.Game.Tests.Visual.Gameplay;
using osu.Game.Tests.Beatmaps.IO;
using osu.Game.Rulesets.Osu;
using osu.Game.Rulesets.Osu.Replays;
using osu.Game.Rulesets.UI;
using osu.Game.Online.Spectator;

namespace osu.Game.Tournament.Tests.Screens
{
    public class TestSceneShowcaseScreen : TournamentTestScene
    {
        [Cached(typeof(SpectatorStreamingClient))]
        private TestSceneSpectator.TestSpectatorStreamingClient testSpectatorStreamingClient = new TestSceneSpectator.TestSpectatorStreamingClient();
        private Spectator spectatorScreen;
        private ShowcaseScreen screen;
        private BeatmapSetInfo importedBeatmap;
        private int importedBeatmapId;
        private int nextFrame;

        [Resolved]
        private TournamentGameBase game { get; set; }

        // used just to show beatmap card for the time being.
        protected override bool UseOnlineAPI => true;
        
        [BackgroundDependencyLoader]
        private void load()
        {
            Add(screen = new ShowcaseScreen(testSpectatorStreamingClient.StreamingUser));
        }

        [SetUpSteps]
        public void SetUpSteps()
        {

            AddStep("reset sent frames", () => nextFrame = 0);

            AddStep("import beatmap", () =>
            {
                importedBeatmap = ImportBeatmapTest.LoadOszIntoOsu(game, virtualTrack: true).Result;
                importedBeatmapId = importedBeatmap.Beatmaps.First(b => b.RulesetID == 0).OnlineBeatmapID ?? -1;
            });

            AddStep("add streaming client", () =>
            {
                Remove(testSpectatorStreamingClient);
                Add(testSpectatorStreamingClient);
            });

            finish();
        }

        [Test]
        public void TestFrameStarvationAndResume()
        {
            loadSpectatingScreen();

            AddAssert("screen hasn't changed", () => Stack.CurrentScreen is Spectator);

            start();
            sendFrames();

            waitForPlayer();
            AddAssert("ensure frames arrived", () => replayHandler.HasFrames);

            AddUntilStep("wait for frame starvation", () => replayHandler.NextFrame == null);
            checkPaused(true);

            double? pausedTime = null;

            AddStep("store time", () => pausedTime = currentFrameStableTime);

            sendFrames();

            AddUntilStep("wait for frame starvation", () => replayHandler.NextFrame == null);
            checkPaused(true);

            AddAssert("time advanced", () => currentFrameStableTime > pausedTime);
        }

        [Test]
        public void TestPlayStartsWithNoFrames()
        {
            loadSpectatingScreen();

            start();
            waitForPlayer();
            checkPaused(true);

            sendFrames(1000); // send enough frames to ensure play won't be paused

            checkPaused(false);
        }

        private void loadSpectatingScreen()
        {
            AddUntilStep("wait for screen stack", () => screen.stack != null);
            AddStep("load screen", () => screen.stack.Push(spectatorScreen = new Spectator(testSpectatorStreamingClient.StreamingUser)));
            AddUntilStep("wait for screen load", () => spectatorScreen.LoadState == LoadState.Loaded);
        }

        private void sendFrames(int count = 10)
        {
            AddStep("send frames", () =>
            {
                testSpectatorStreamingClient.SendFrames(nextFrame, count);
                nextFrame += count;
            });
        }

          [Test]
        public void TestSpectatingDuringGameplay()
        {
            start();

            loadSpectatingScreen();

            AddStep("advance frame count", () => nextFrame = 300);
            sendFrames();

            waitForPlayer();

            AddUntilStep("playing from correct point in time", () => player.ChildrenOfType<DrawableRuleset>().First().FrameStableClock.CurrentTime > 30000);
        }

        [Test]
        public void TestHostRetriesWhileWatching()
        {
            loadSpectatingScreen();

            start();
            sendFrames();

            waitForPlayer();

            Player lastPlayer = null;
            AddStep("store first player", () => lastPlayer = player);

            start();
            sendFrames();

            waitForPlayer();
            AddAssert("player is different", () => lastPlayer != player);
        }

        [Test]
        public void TestHostFails()
        {
            loadSpectatingScreen();

            start();

            waitForPlayer();
            checkPaused(true);

            finish();

            checkPaused(false);
            // TODO: should replay until running out of frames then fail
        }

        [Test]
        public void TestStopWatchingDuringPlay()
        {
            loadSpectatingScreen();

            start();
            sendFrames();
            waitForPlayer();

            AddStep("stop spectating", () => (Stack.CurrentScreen as Player)?.Exit());
            AddUntilStep("spectating stopped", () => spectatorScreen.GetChildScreen() == null);
        }

        [Test]
        public void TestStopWatchingThenHostRetries()
        {
            loadSpectatingScreen();

            start();
            sendFrames();
            waitForPlayer();

            AddStep("stop spectating", () => (Stack.CurrentScreen as Player)?.Exit());
            AddUntilStep("spectating stopped", () => spectatorScreen.GetChildScreen() == null);

            // host starts playing a new session
            start();
            waitForPlayer();
        }

        [Test]
        public void TestWatchingBeatmapThatDoesntExistLocally()
        {
            loadSpectatingScreen();

            start(-1234);
            sendFrames();

            AddAssert("screen didn't change", () => Stack.CurrentScreen is Spectator);
        }

        private OsuFramedReplayInputHandler replayHandler =>
            (OsuFramedReplayInputHandler)Stack.ChildrenOfType<OsuInputManager>().First().ReplayInputHandler;

        private double currentFrameStableTime
            => player.ChildrenOfType<FrameStabilityContainer>().First().FrameStableClock.CurrentTime;

        private Player player => Stack.CurrentScreen as Player;

        private ScreenStack Stack => screen.stack;

        private void waitForPlayer() => AddUntilStep("wait for player", () => screen.stack.CurrentScreen is Player);

        private void start(int? beatmapId = null) => AddStep("start play", () => testSpectatorStreamingClient.StartPlay(beatmapId ?? importedBeatmapId));

        private void finish(int? beatmapId = null) => AddStep("end play", () => testSpectatorStreamingClient.EndPlay(beatmapId ?? importedBeatmapId));

        private void checkPaused(bool state) =>
            AddUntilStep($"game is {(state ? "paused" : "playing")}", () => player.ChildrenOfType<DrawableRuleset>().First().IsPaused.Value == state);


    }
}

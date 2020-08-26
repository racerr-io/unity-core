using Mirror;
using Racerr.Gameplay.Car;
using Racerr.Utility;
using Racerr.World.Track;
using System;
using System.Linq;
using UnityEngine;

namespace Racerr.Infrastructure.Server
{
    /// <summary>
    /// Manages the currently running race. Keeps track of all the Players in the race and
    /// ensures transition to Idle / Intermission once all players have left or finished the race.
    /// <remarks>
    /// Assumes the track has already been generated before transitioning into this state.
    /// </remarks>
    /// </summary>
    public class ServerRaceState : RaceSessionState
    {
        const int secondsPerTrack = 6;

        public int MaxRaceDuration => TrackGenerator.Singleton.TrackLength * secondsPerTrack;
        [SerializeField] int remainingRaceDurationOnPlayerFinish = 30;

        [SyncVar] double raceFinishTime;
        public double RemainingRaceTime => raceFinishTime - NetworkTime.time;

        /// <summary>
        /// Initialises brand new race session data independent of previous race sessions.
        /// </summary>
        [Server]
        public override void Enter(object optionalData = null)
        {
            raceSessionData = new RaceSessionData(NetworkTime.time);
            raceFinishTime = NetworkTime.time + MaxRaceDuration;
            EnableAllPlayerCarControllers();
        }

        /// <summary>
        /// When exiting the race, destroy any players which remain on the track. 
        /// Note that players remain on the track after the race from dying (their uncontrollable corpse remains).
        /// </summary>
        [Server]
        public override void Exit()
        {
            foreach (Player player in raceSessionData.PlayersInRace)
            {
                player.DestroyAllCarsForPlayer();
            }
        }

        /// <summary>
        /// By default, cars are instantiated in disabled state, meaning any input from the client's controller is ignored
        /// and the car won't move. This is done because we don't want people driving while the track is generating before the race
        /// has started. This function will allow car's to be driven, as we have now entered the Race State.
        /// </summary>
        [Server]
        void EnableAllPlayerCarControllers()
        {
            foreach (Player player in raceSessionData.PlayersInRace.Where(player => player.Car != null))
            {
                player.Car.SetIsActive(true);
            }
        }

        /// <summary>
        /// Server side only - call this function once the car finishes the race so that
        /// their car is removed and they are marked as finished.
        /// </summary>
        /// <param name="player">The player that finished.</param>
        [ServerCallback]
        void NotifyPlayerFinished(Player player)
        {
            raceSessionData.FinishedPlayers.Add(player);
            player.PosInfo = new Player.PositionInfo(player.PosInfo.startTime, NetworkTime.time);
            player.MarkPlayerCarAsZombie();
            SentrySdk.AddBreadcrumb($"{ player.PlayerName } finished the race.");
        }

        /// <summary>
        /// Server side only - call this function when the car moves through a checkpoint.
        /// Adds checkpoint to a set of checkpoints the player has passed through so that
        /// we can calculate their position. We ignore players passing through checkpoints
        /// when they are not a racer (e.g. they are police). Instead, we check if police
        /// cars have left the finish line to avoid spawning them in the same position.
        /// Additionally, check if the player has actually finished the race.
        /// </summary>
        /// <param name="player">The player that passed through.</param>
        /// <param name="checkpoint">The checkpoint the player hit.</param>
        [ServerCallback]
        public void NotifyPlayerPassedThroughCheckpoint(Player player, GameObject checkpoint)
        {
            if (player.Car == null || ServerStateMachine.Singleton.StateType != StateEnum.Race)
            {
                return;
            }
            else if (player.Car.CarType == CarManager.CarTypeEnum.Racer)
            {
                player.PosInfo.Checkpoints.Add(checkpoint);

                if (checkpoint.name == GameObjectIdentifiers.FinishLineCheckpoint)
                {
                    NotifyPlayerFinished(player);
                }
            }
            else if (player.Car.CarType == CarManager.CarTypeEnum.Police)
            {
                SpawnManager.Singleton.NotifyPlayerPoliceCarNotOnFinishingGrid(player);
            }
        }

        /// <summary>
        /// Called every game tick.
        /// Changes the remaining race time if there are players finished.
        /// Checks whether or not to transition to intermission state, based on if the race is finished or empty.
        /// </summary>
        [Server]
        void FixedUpdate()
        {
            UpdateRaceFinishTimeIfAnyPlayerFinished();

            bool isRaceFinished = raceSessionData.FinishedPlayers.Count + raceSessionData.DeadAsRacerPlayers.Count == raceSessionData.PlayersInRace.Count || RemainingRaceTime <= 0;
            bool isRaceHasNoHumanPlayers = raceSessionData.PlayersInRace.Where(player => !player.IsAI).Count() == 0;

            if (isRaceHasNoHumanPlayers)
            {
                TransitionToIdle();
            }
            else if (isRaceFinished)
            {
                TransitionToIntermission();
            }

            UpdateLeaderboard(); // Ensure clients have a live updated view of the leaderboard always.
        }

        /// <summary>
        /// Lowers the time the race must be finished by as soon as one player has finished - we don't want to wait too long for stragglers. 
        /// This is done by taking the current server time and adding a predefined duration. 
        /// If it turns out that the race has nearly finished, the finish time remains unchanged, to prevent extending the remaining race time.
        /// </summary>
        [Server]
        void UpdateRaceFinishTimeIfAnyPlayerFinished()
        {
            if (raceSessionData.FinishedPlayers.Any())
            {
                raceFinishTime = Math.Min(raceFinishTime, NetworkTime.time + remainingRaceDurationOnPlayerFinish);
            }
        }

        
        [Server]
        void TransitionToIntermission()
        {
            // Copy over the Race Session Data to the Intermission State so the previous race's leaderboard and timer
            // is shown while players wait for the next race.
            RaceSessionData raceSessionDataForIntermission = new RaceSessionData(
                raceSessionData.raceStartTime,
                CurrentRaceDuration,
                raceSessionData.PlayersInRace,
                raceSessionData.FinishedPlayers
            );
            ServerStateMachine.Singleton.ChangeState(StateEnum.Intermission, raceSessionDataForIntermission);
        }

        [Server]
        void TransitionToIdle()
        {
            ServerStateMachine.Singleton.ChangeState(StateEnum.ServerIdle);
        }
    }
}


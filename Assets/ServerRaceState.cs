﻿using Mirror;
using Racerr.MultiplayerService;
using Racerr.Track;
using System.Collections;
using UnityEngine;

namespace Racerr.StateMachine.Server
{
    public class ServerRaceState : RaceSessionState
    {
        /// <summary>
        /// Initialises brand new race session data independant of previous race sessions.
        /// Then starts generating the track, which will then start the race.
        /// </summary>
        public override void Enter(object optionalData = null)
        {
            raceSessionData = new RaceSessionData();
            StartRace();
        }

        /// <summary>
        /// Procedure to actually setup and start the race.
        /// Called only after track is generated.
        /// </summary>
        void StartRace()
        {
            Vector3 currPosition = new Vector3(0, 1, 10);
            raceSessionData.PlayersInRace.AddRange(ServerStateMachine.Singleton.ReadyPlayers);

            foreach (Player player in raceSessionData.PlayersInRace)
            {
                player.CreateCarForPlayer(currPosition);
                player.PositionInfo = new PlayerPositionInfo();
                currPosition += new Vector3(0, 0, 10);
            }

            //raceStartTime = NetworkTime.time;
            //isCurrentlyRacing = true;
        }

        /// <summary>
        /// Called every game tick.
        /// Checks whether or not to transition to intermission state, based on if the race is finished or empty.
        /// </summary>
        void LateUpdate()
        {
            bool isRaceFinished = raceSessionData.FinishedPlayers.Count + raceSessionData.DeadPlayers.Count == raceSessionData.PlayersInRace.Count;
            bool isRaceEmpty = raceSessionData.PlayersInRace.Count == 0;

            if (isRaceEmpty)
            {
                TransitionToIdle();
            }
            else if (isRaceFinished)
            {
                TransitionToIntermission();
            }
        }

        public void TransitionToIntermission()
        {
            ServerStateMachine.Singleton.ChangeState(StateEnum.Intermission, raceSessionData);
        }

        public void TransitionToIdle()
        {
            ServerStateMachine.Singleton.ChangeState(StateEnum.ServerIdle);
        }
    }
}


﻿using Mirror;
using Racerr.Car.Core;
using UnityEngine;

namespace Racerr.MultiplayerService
{
    /// <summary>
    /// Custom designed interpolation for car driving.
    /// </summary>
    [RequireComponent(typeof(PlayerCarController))]
    public class RacerrCarNetworkTransform : NetworkBehaviour
    {
        [SerializeField] [Range(0, 1)] float interpolationFactor = 0.4f;
        [SyncVar] Vector3 realPosition = Vector3.zero;
        [SyncVar] Quaternion realRotation;
        [SyncVar] Vector3 realVelocity;
        new Rigidbody rigidbody;

        /// <summary>
        /// Called when car is instantiated. If car is someone elses car update the rigidbody and remove the wheel colliders
        /// as they intefere with movement.
        /// </summary>
        void Start()
        {
            rigidbody = GetComponent<Rigidbody>();

            if (!hasAuthority)
            {
                rigidbody.isKinematic = true;
                rigidbody.useGravity = false;

                foreach (WheelCollider wheelCollider in GetComponentsInChildren<WheelCollider>())
                {
                    Destroy(wheelCollider);
                }
            }
        }

        /// <summary>
        /// Called every physics tick to update car's position.
        /// </summary>
        void FixedUpdate()
        {
            if (hasAuthority)
            {
                realPosition = transform.position;
                realRotation = transform.rotation;
                realVelocity = rigidbody.velocity;
                CmdSynchroniseToServer(transform.position, transform.rotation, rigidbody.velocity);
            }
            else
            {
                Vector3 predictedPosition = realPosition + Time.deltaTime * realVelocity; // Try to predict where the car might be. TODO: Incorporate difference in network time and local time.
                transform.position = Vector3.Lerp(transform.position, predictedPosition, interpolationFactor);
                transform.rotation = Quaternion.Lerp(transform.rotation, realRotation, interpolationFactor);
                rigidbody.velocity = realVelocity;
            }
        }

        /// <summary>
        /// A command to the server which updates the variables on the server. Updating the variables on the server cause
        /// variables on all clients to be synchronised.
        /// </summary>
        /// <param name="position">Car actual position.</param>
        /// <param name="rotation">Car actual rotation.</param>
        /// <param name="velocity">Car actual velocity.</param>
        [Command]
        void CmdSynchroniseToServer(Vector3 position, Quaternion rotation, Vector3 velocity)
        {
            realPosition = position;
            realRotation = rotation;
            realVelocity = velocity;
        }
    }
}
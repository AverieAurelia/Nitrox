using NitroxClient.Communication.Abstract;
using NitroxClient.MonoBehaviours;
using NitroxModel.DataStructures;
using UnityEngine;

namespace NitroxClient.GameLogic
{
    public class LiveMixinManager
    {

        private readonly IMultiplayerSession multiplayerSession;
        private readonly SimulationOwnership simulationOwnership;

        private bool processingRemoteHealthChange = false;

        public LiveMixinManager(IMultiplayerSession multiplayerSession, SimulationOwnership simulationOwnership)
        {
            this.multiplayerSession = multiplayerSession;
            this.simulationOwnership = simulationOwnership;
        }

        // Currently, we only apply live mixin updates to vehicles as there is more work to implement
        // damage for regular entities like fish.
        public bool IsWhitelistedUpdateType(LiveMixin entity)
        {
            Vehicle vehicle = entity.GetComponent<Vehicle>();
            SubRoot subRoot = entity.GetComponent<SubRoot>();

            return (vehicle != null || (subRoot != null && subRoot.isCyclops));
        }

        public bool ShouldApplyNextHealthUpdate(LiveMixin reciever, GameObject dealer = null)
        {
            NitroxId id = NitroxEntity.GetId(reciever.gameObject);

            if (!simulationOwnership.HasAnyLockType(id) && !processingRemoteHealthChange)
            {
                return false;
            }

            // Check to see if this health change is caused by docked vehicle collisions.  If so, we don't want to apply it.
            if (dealer)
            {
                Vehicle dealerVehicle = dealer.GetComponent<Vehicle>();
                VehicleDockingBay vehicleDockingBay = reciever.GetComponentInChildren<VehicleDockingBay>();

                if (vehicleDockingBay && dealerVehicle)
                {
                    if (vehicleDockingBay.GetDockedVehicle() == dealerVehicle || vehicleDockingBay.interpolatingVehicle == dealerVehicle
                        || vehicleDockingBay.nearbyVehicle == dealerVehicle)
                    {
                        Log.Debug($"Dealer {dealer} is vehicle and currently docked or nearby {reciever}, do not harm it!");
                        return false;
                    }
                }
            }

            return true;
        }

        public void SyncRemoteHealth(LiveMixin liveMixin, float remoteHealth)
        {
            if (liveMixin.health == remoteHealth)
            {
                return;
            }

            float difference = remoteHealth - liveMixin.health;

            processingRemoteHealthChange = true;

            if (difference < 0)
            {
                liveMixin.TakeDamage(difference);
            }
            else
            {
                liveMixin.AddHealth(difference);
            }

            processingRemoteHealthChange = false;

            // We mainly only do the above to trigger damage effects and sounds.  After those, we sync the remote value
            // to ensure that any floating point discrepencies aren't an issue.
            liveMixin.health = remoteHealth;
        }

    }
}

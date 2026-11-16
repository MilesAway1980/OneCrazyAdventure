using System.Collections.Generic;
using Mirror;
using UnityEngine;

namespace Characters
{
    // Takes a character out of use, in one of two ways, and puts them back.
    //
    //  - Put away: the character isn't in the game. They can't be seen, are solid to nothing, don't move and don't do anything.
    //  - Shown only: the character is in the game, but on a machine that joined someone else's. Their game is simulated by the host,
    //    so here they are only a picture that is moved to where the host says. They can be seen, but nothing else about them is
    //    working: no scripts, no colliders.
    //
    // It's done this way rather than by making the character's object inactive because in a game played over a network every
    // character has to exist on every machine all the time: Mirror wakes up and spawns anything inactive in a scene, and
    // brings an object that was switched off back on. So a character is never switched off. Everything is taken out of use
    // instead, and what was in use is remembered so that exactly that is put back.
    //
    // It happens at the end of the frame, after everything has had its Start, which is what lets a character be put away from
    // the very beginning.
    public class CharacterParking : MonoBehaviour
    {
        private enum Mode
        {
            Normal,
            ShownOnly,
            PutAway
        }

        private bool wantPutAway;
        private bool wantShownOnly;
        private Mode applied = Mode.Normal;

        private readonly List<Behaviour> behaviours = new List<Behaviour>();
        private readonly List<Renderer> renderers = new List<Renderer>();
        private readonly List<Collider2D> colliders = new List<Collider2D>();

        private Rigidbody2D body;
        private bool bodyWasSimulated;

        // Put away, which wins over being shown only.
        public void SetParked(bool park)
        {
            wantPutAway = park;
        }

        // On a machine that is only showing what the host simulates.
        public void SetShownOnly(bool shownOnly)
        {
            wantShownOnly = shownOnly;
        }

        void LateUpdate()
        {
            var wanted = wantPutAway ? Mode.PutAway : wantShownOnly ? Mode.ShownOnly : Mode.Normal;

            if (wanted == applied)
            {
                return;
            }

            if (applied != Mode.Normal)
            {
                Restore();
            }

            if (wanted != Mode.Normal)
            {
                TakeOutOfUse(wanted);
            }

            applied = wanted;
        }

        private void TakeOutOfUse(Mode mode)
        {
            // Everything that's doing something. Anything already switched off is left alone, so it's still off when they're back.
            foreach (var behaviour in GetComponentsInChildren<Behaviour>(false))
            {
                // This keeps the character on the network, so it's not part of what's put away.
                if (behaviour == this || !behaviour.enabled || behaviour is NetworkBehaviour || behaviour is NetworkIdentity)
                {
                    continue;
                }

                // Making an object inactive would have stopped these. Switching a script off doesn't.
                if (behaviour is MonoBehaviour mono)
                {
                    mono.StopAllCoroutines();
                    mono.CancelInvoke();
                }

                behaviour.enabled = false;
                behaviours.Add(behaviour);
            }

            foreach (var collider in GetComponentsInChildren<Collider2D>(false))
            {
                if (collider.enabled)
                {
                    collider.enabled = false;
                    colliders.Add(collider);
                }
            }

            // A character that's shown only stays visible, and its body is left to the network, which moves it.
            if (mode == Mode.PutAway)
            {
                foreach (var renderer in GetComponentsInChildren<Renderer>(false))
                {
                    if (renderer.enabled)
                    {
                        renderer.enabled = false;
                        renderers.Add(renderer);
                    }
                }

                body = GetComponent<Rigidbody2D>();
                bodyWasSimulated = body && body.simulated;

                if (bodyWasSimulated)
                {
                    body.simulated = false;
                }
            }

            // Dust and the like are cleared, so there's none left hanging in the air for when they're back.
            foreach (var particles in GetComponentsInChildren<ParticleSystem>(false))
            {
                particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
        }

        private void Restore()
        {
            if (bodyWasSimulated && body)
            {
                body.simulated = true;
            }

            bodyWasSimulated = false;

            foreach (var collider in colliders)
            {
                if (collider)
                {
                    collider.enabled = true;
                }
            }

            foreach (var renderer in renderers)
            {
                if (renderer)
                {
                    renderer.enabled = true;
                }
            }

            foreach (var behaviour in behaviours)
            {
                if (behaviour)
                {
                    behaviour.enabled = true;
                }
            }

            colliders.Clear();
            renderers.Clear();
            behaviours.Clear();
        }
    }
}

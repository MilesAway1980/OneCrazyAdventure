using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Characters
{
    public class Magnet : Character
    {
        [SerializeField]
        private GameObject magneticField;
        private bool magnetActive;

        protected override void Start()
        {
            magneticField.SetActive(false);
            base.Start();
        }

        protected override void Update()
        {
            base.Update();

            if (!magnetActive)
            {
                canMove = true;
                magneticField.SetActive(false);
            }
            else
            {
                magneticField.SetActive(true);
            }
        }

        public override void Action(bool active)
        {
            performingAction = active;
            magnetActive = active;
        }

        public override void DirectionPress(Vector2 directionValue)
        {
            if (canMove)
            {
                Move(directionValue);
            }
        }
    }
}
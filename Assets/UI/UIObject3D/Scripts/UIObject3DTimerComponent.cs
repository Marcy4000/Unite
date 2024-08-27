using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections.Generic;
using System.Linq;

namespace UI.ThreeDimensional
{
    public class UIObject3DTimerComponent : MonoBehaviour
    {
        public List<DelayedAction> delayedActions = new List<DelayedAction>();

        public void DelayedCall(float delay, Action action, MonoBehaviour target, bool forceEvenIfTargetIsInactive)
        {
            this.enabled = true;

            delayedActions.Add(new DelayedAction { timeToExecute = Time.unscaledTime + delay, action = action, target = target, forceEvenIfTargetIsInactive = forceEvenIfTargetIsInactive });
        }

        private void Update()
        {
            List<DelayedAction> actionsToExecute = null;
            foreach (var action in delayedActions)
            {
                if (Time.unscaledTime >= action.timeToExecute)
                {
                    if (actionsToExecute == null) actionsToExecute = new List<DelayedAction>();
                    actionsToExecute.Add(action);
                }
            }

            if (actionsToExecute == null || actionsToExecute.Count == 0) return;

            foreach (var action in actionsToExecute)
            {
                try
                {
                    if ((action.forceEvenIfTargetIsInactive)
                     || (action.target != null && action.target.gameObject.activeInHierarchy))
                    {
                        action.action.Invoke();
                    }
                }
                finally
                {
                    delayedActions.Remove(action);
                }
            }

            // stop calling update if we have nothing scheduled (DelayedCall will re-enable this)
            if (delayedActions.Count == 0) this.enabled = false;
        }
    }

    public class DelayedAction
    {
        public float timeToExecute;
        public Action action;
        public MonoBehaviour target;
        public bool forceEvenIfTargetIsInactive;
    }
}

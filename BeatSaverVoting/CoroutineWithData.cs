using System.Collections;
using UnityEngine;

namespace BeatSaverVoting
{
    public class CoroutineWithData {
        public Coroutine Coroutine { get; }
        public object Result;
        private readonly IEnumerator _target;
        public CoroutineWithData(MonoBehaviour owner, IEnumerator target) {
            _target = target;
            Coroutine = owner.StartCoroutine(Run());
        }

        private IEnumerator Run() {
            while(_target.MoveNext()) {
                Result = _target.Current;
                yield return Result;
            }
        }
    }
}
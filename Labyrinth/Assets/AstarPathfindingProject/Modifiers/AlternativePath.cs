using UnityEngine;

namespace Pathfinding {
	[AddComponentMenu("Pathfinding/Modifiers/Alternative Path")]
	[System.Serializable]
	/** Applies penalty to the paths it processes telling other units to avoid choosing the same path.
	 *
	 * Note that this might not work properly if penalties are modified by other actions as well (e.g graph update objects which reset the penalty to zero).
	 * It will only work when all penalty modifications are relative, i.e adding or subtracting penalties, but not when setting penalties
	 * to specific values.
	 *
	 * When destroyed, it will correctly remove any added penalty. However it will only be done before the next
	 * path request is calculated, so if this was the last path requesting unit to be destroyed, the penalty will stay
	 * in the graph until the next path request is made.
	 *
	 * \ingroup modifiers
	 */
	[HelpURL("http://arongranberg.com/astar/docs/class_pathfinding_1_1_alternative_path.php")]
	public class AlternativePath : MonoModifier {
	#if UNITY_EDITOR
		[UnityEditor.MenuItem("CONTEXT/Seeker/Add Alternative Path Modifier")]
		public static void AddComp (UnityEditor.MenuCommand command) {
			(command.context as Component).gameObject.AddComponent(typeof(AlternativePath));
		}
	#endif

		public override int Order { get { return 10; } }

		/** How much penalty (weight) to apply to nodes */
		public int penalty = 1000;

		/** Max number of nodes to skip in a row */
		public int randomStep = 10;

		/** The previous path */
		GraphNode[] prevNodes;

		/** Previous seed. Used to figure out which nodes to revert penalty on without storing them in an array */
		int prevSeed;

		/** The previous penalty used. Stored just in case it changes during operation */
		int prevPenalty;

		bool waitingForApply;

		readonly System.Object lockObject = new System.Object();

		/** A random object */
		System.Random rnd = new System.Random();

		/** A random object generating random seeds for other random objects */
		readonly System.Random seedGenerator = new System.Random();

		bool destroyed;

		/** The nodes waiting to have their penalty changed */
		GraphNode[] toBeApplied;
		public override void Apply (Path p) {
			if (this == null) return;

			lock (lockObject) {
				toBeApplied = p.path.ToArray();

				if (!waitingForApply) {
					waitingForApply = true;
					AstarPath.OnPathPreSearch += ApplyNow;
				}
			}
		}

		public override void OnDestroy () {
			destroyed = true;
			lock (lockObject) {
				if (!waitingForApply) {
					waitingForApply = true;
					AstarPath.OnPathPreSearch += ClearOnDestroy;
				}
			}
			base.OnDestroy();
		}

		void ClearOnDestroy (Path p) {
			lock (lockObject) {
				AstarPath.OnPathPreSearch -= ClearOnDestroy;
				waitingForApply = false;
				InversePrevious();
			}
		}

		void InversePrevious () {
			int seed = prevSeed;

			rnd = new System.Random(seed);

			//Add previous penalty
			if (prevNodes != null) {
				bool warnPenalties = false;
				int rndStart = rnd.Next(randomStep);
				for (int i = rndStart; i < prevNodes.Length; i += rnd.Next(1, randomStep)) {
					if (prevNodes[i].Penalty < prevPenalty) {
						warnPenalties = true;
						prevNodes[i].Penalty = 0;
					} else {
						prevNodes[i].Penalty = (uint)(prevNodes[i].Penalty-prevPenalty);
					}
				}
				if (warnPenalties) {
					Debug.LogWarning("Penalty for some nodes has been reset while this modifier was active. Penalties might not be correctly set.", this);
				}
			}
		}

		void ApplyNow (Path somePath) {
			lock (lockObject) {
				waitingForApply = false;
				AstarPath.OnPathPreSearch -= ApplyNow;

				InversePrevious();

				if (destroyed) return;

				//Calculate a new seed
				int seed = seedGenerator.Next();
				rnd = new System.Random(seed);

				if (toBeApplied != null) {
					int rndStart = rnd.Next(randomStep);
					for (int i = rndStart; i < toBeApplied.Length; i += rnd.Next(1, randomStep)) {
						toBeApplied[i].Penalty = (uint)(toBeApplied[i].Penalty+penalty);
					}
				}

				prevPenalty = penalty;
				prevSeed = seed;
				prevNodes = toBeApplied;
			}
		}
	}
}

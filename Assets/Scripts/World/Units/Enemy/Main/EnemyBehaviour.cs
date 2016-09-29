﻿/*
 * Author(s): Isaiah Mann
 * Description: Actions of an in game enemy
 */

using UnityEngine;
using System.Collections;

public class EnemyBehaviour : MobileAgentBehaviour {
	public Direction DirectionFacing;
	TowerBehaviour currentTarget;
	Enemy enemy;
	public override string IName {
		get {
			return enemy.Type;
		}
	}
	public override float IAttackDelay {
		get {
			return enemy.AttackCooldown;
		}
	}
	public void SetEnemy (Enemy enemy) {
		this.enemy = enemy;
	}

	protected override void SetReferences() {
		base.SetReferences();
    }

	protected override void FetchReferences() {}

	protected override void CleanupReferences () {
		base.CleanupReferences();
		EventController.Event(EventType.EnemyDestroyed);
	}

    protected override void HandleNamedEvent(string eventName) {}

		
	void ResumeMoving () {
		if (WorldController.Instance && WorldController.Instance.ICoreOrbInstance != null) {
			SetTarget(WorldController.Instance.ICoreOrbInstance);
		}
	}

    public override void MoveTo(MapLocation location) {
		if (WorldController.Instance && WorldController.Instance.ICoreOrbInstance != null) {
			SetTarget(WorldController.Instance.ICoreOrbInstance);
		}
    }

	public override ActiveObjectBehaviour SelectTarget() {
		throw new System.NotImplementedException();
	}

	public void SetTarget (GameObject target) {
		if (this != null && gameObject != null) {
			if (gameObject.activeInHierarchy) {
				StartCoroutine(movementCoroutine = MoveTowardsTarget(target));
				isMoving = true;
			}
		}
	}

	public void Halt () {
		if (movementCoroutine != null) {
			StopCoroutine(movementCoroutine);
			isMoving = false;
		}
	}

	public override void Attack (ActiveObjectBehaviour activeAgent, int damage) {
		base.Attack (activeAgent, damage);
		EventController.Event(EventType.EnemiesDealDamage);
	}

	public override void Damage (int damage) {
		base.Damage (damage);
		EventController.Event(EventType.EnemiesTakeDamage);
	}

	IEnumerator MoveTowardsTarget (GameObject target) {
		float stop = Random.Range(0.1f, 0.5f);
		while (target != null && Vector3.Distance(transform.position, target.transform.position) > stop) {
			transform.position = Vector3.MoveTowards(transform.position, target.transform.position, 0.05f);
			yield return new WaitForEndOfFrame();
		}
		yield return new WaitForEndOfFrame();
	}

	void OnTriggerEnter (Collider collider) {
		if (CanAttack(collider)) {
			TowerBehaviour tower = collider.GetComponent<TowerBehaviour>();
			Halt();
			Attack(tower, enemy.AttackDamage);
			tower.SubscribeToDestruction(ResumeMoving);
		}
	}

	void OnTriggerStay (Collider collider) {
		if (CanAttack(collider)) {
			currentTarget = collider.GetComponent<TowerBehaviour>();
			Halt();
			Attack(currentTarget, enemy.AttackDamage);
		}
	}


	bool CanAttack (Collider unit) {
		return !attackCooldownActive && unit.tag == TowerController.TOWER_TAG;
	}
}

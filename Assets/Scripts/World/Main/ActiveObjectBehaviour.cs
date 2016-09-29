﻿/*
 * Author(s): Isaiah Mann
 * Description: Template behaviour for active word objects (can interact w/ other objects in the world and affect the game logic)
 */

using UnityEngine;
using System.Collections;

public abstract class ActiveObjectBehaviour : WorldObjectBehaviour {
	public string Name;
	public int Health;
	public int MaxHealth;
	public int BaseDamage;
	public int Range;
	public string LevelString;
	public float AttackDelay;
	public virtual float IAttackDelay {
		get {
			return AttackDelay;
		}
	}
	public virtual string IName {
		get {
			return Name;
		}
	}
	EventAction onDestroyed;
	[SerializeField]
	protected bool HasAttack;

	[SerializeField]
	protected HealthBarBehaviour HealthBar;

	protected bool attackCooldownActive = false;

	[SerializeField]
	bool debugging;

	IUnit linkedObject;
	public IUnit ILinkedObject {
		get {
			return linkedObject;
		}
	}
		
	void SetStats () {
	}

	protected override void SetReferences () {
		SetStats();
	}

	protected override void CleanupReferences () {
		if (onDestroyed != null) {	
			onDestroyed();
		}
	}
		
	public virtual void Attack(ActiveObjectBehaviour activeAgent, int damage) {
		StartCoroutine(AttackCooldown());
		activeAgent.Damage(damage);
	}

	public abstract ActiveObjectBehaviour SelectTarget();

	public virtual void Damage(int damage) {
		Health -= damage;
		if (HealthBar) {
			HealthBar.SetHealthDisplay(
				(float) Health /
				(float) MaxHealth
			);
		}
		if (Health <= 0) {
			Destroy();
		}
	}
    
	public virtual void Heal(int healthPoints) {
		Health += healthPoints;
	}
    
	public virtual void Destroy() {
		DestroyObject(gameObject);
	}

	public virtual bool InRange(ActiveObjectBehaviour activeAgent) {
		return (Range >= MapLocation.Distance(Location, activeAgent.Location));
	}

	public void ReceiveLink (IUnit unit) {
		linkedObject = unit;
	}

	public void SeverLink () {
		linkedObject = null;
	}

	public bool HasLink () {
		return linkedObject != null;
	}

	protected IEnumerator AttackCooldown () {
		attackCooldownActive = true;
		yield return new WaitForSeconds(IAttackDelay);
		attackCooldownActive = false;
	}

	public void ToggleColliders (bool areCollidersEnabled) {
		foreach (Collider collider in GetComponents<Collider>()) {
			collider.enabled = areCollidersEnabled;
		}
	}

	public void SubscribeToDestruction (EventAction action) {
		onDestroyed += action;
	}

	public void UnusubscribeFromDestruction (EventAction action) {
		onDestroyed -= action;
	}
		
	public virtual void HandleColliderEnterTrigger (Collider collider) {}

	public virtual void HandleColliderStayTrigger (Collider collider) {}

	public virtual void HandleColliderExitTrigger (Collider collider) {}
}

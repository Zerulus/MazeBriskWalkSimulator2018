﻿// Projectile skill effects like arrows, flaming fire balls, etc. that deal
// damage on the target.
//
// Note: we could move it on the server and use NetworkTransform to synchronize
// the position to all clients, which is the easy method. But we just move it on
// the server and the on the client to save bandwidth. Same result.
using UnityEngine;
using Mirror;

public class ProjectileSkillEffect : SkillEffect
{
    public float speed = 1;
    [HideInInspector] public int damage = 1; // set by skill
    [HideInInspector] public float stunChance; // set by skill
    [HideInInspector] public float stunTime; // set by skill
    private bool hasSetTime = false;
    private float now;

    // update here already so that it doesn't spawn with a weird rotation
    void Start() { 
        now = Time.time;    
        FixedUpdate(); 
    }

    // fixedupdate on client and server to simulate the same effect without
    // using a NetworkTransform
    void FixedUpdate()
    {
        if(caster.isFirstPerson) {
            if(target != null && caster != null) {               
                if(isServer) {
                    if(target.health > 0 && !hasSetTime) {
                        caster.DealDamageAt(target, caster.damage + damage, stunChance, stunTime);
                    }
                    if(transform.position == target.transform.position) {
                        transform.parent = target.transform;
                    }

                    now = SetTime(now, hasSetTime);
                    if(now + 1f <= Time.time) {
                        NetworkServer.Destroy(gameObject);
                    }
                        
                }
            } else if (isServer) NetworkServer.Destroy(gameObject);
        } else {
            // target and caster still around?
            // note: we keep flying towards it even if it died already, because
            //       it looks weird if fireballs would be canceled inbetween.
            if (target != null && caster != null)
            {
                // move closer and look at the target
                Vector3 goal = target.collider.bounds.center;
                transform.position = Vector3.MoveTowards(transform.position, goal, speed);
                transform.LookAt(goal);

                // server: reached it? apply skill and destroy self
                if (isServer && transform.position == goal)
                {
                    if (target.health > 0)
                    {
                        // find the skill that we casted this effect with
                        caster.DealDamageAt(target, caster.damage + damage, stunChance, stunTime);
                    }
                    NetworkServer.Destroy(gameObject);
                }
            }
            else if (isServer) NetworkServer.Destroy(gameObject);
        }

    }

    private float SetTime(float now, bool hasSetTime) {
        if(!hasSetTime) {
            this.hasSetTime = true;
            return Time.time;
        }

        return now;
    }


}

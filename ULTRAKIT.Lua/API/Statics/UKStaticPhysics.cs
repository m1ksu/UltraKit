﻿using MoonSharp.Interpreter;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ULTRAKIT.Lua.API.Abstract;
using ULTRAKIT.Lua.API.Proxies.Components;
using UnityEngine;

namespace ULTRAKIT.Lua.API.Statics
{
    [MoonSharpUserData]
    public class UKHitResult
    {
        public Vector3 point;
        public Vector3 normal;
        public UKProxyEnemy enemy;
        public UKProxyProjectile projectile;
        public Transform transform;
        public GameObject gameObject;

        public UKHitResult(Vector3 point, Vector3 normal, Transform transform, UKProxyEnemy enemy, UKProxyProjectile projectile)
        {
            this.point = point;
            this.normal = normal;
            this.enemy = enemy;
            this.projectile = projectile;
            this.transform = transform;
            this.gameObject = transform.gameObject;
        }

        public UKHitResult(UKHitResult other)
        {
            this.point = other.point;
            this.normal = other.normal;
            this.transform = other.transform;
            this.gameObject = other.gameObject;
            this.enemy = other.enemy;
            this.projectile = other.projectile;
        }
    }

    public class UKStaticPhysics : UKStatic
    {
        public override string name => "Physics";

        // EnemyTrigger, Projectile, Environment
        const int DefaultCastMask = (1 << 12) | (1 << 14) | (1 << 8);
        
        public Collider[] OverlapSphere(Vector3 pos, float radius, int layermask, bool triggerinteraction = false)
        {
            if (triggerinteraction)
            {
                return Physics.OverlapSphere(pos, radius, layermask, QueryTriggerInteraction.Collide);
            }
            else
            {
                return Physics.OverlapSphere(pos, radius, layermask, QueryTriggerInteraction.Ignore);
            }
        }
        public Collider[] OverlapSphere(Vector3 pos, float radius, int layermask) => Physics.OverlapSphere(pos, radius, layermask);
        public Collider[] OverlapSphere(Vector3 pos, float radius) => Physics.OverlapSphere(pos, radius);

        public UKHitResult Raycast(Vector3 point, Vector3 dir, float maxDistance = Mathf.Infinity, int layerMask = DefaultCastMask, bool ignoreTriggers = true)
        {
            RaycastHit hit;
            if (Physics.Raycast(point, dir, out hit, maxDistance, layerMask, ignoreTriggers ? QueryTriggerInteraction.Ignore : QueryTriggerInteraction.Collide))
            {
                var enemyIdentifier = hit.transform.GetComponentInChildren<EnemyIdentifier>() ?? hit.transform.GetComponentInParent<EnemyIdentifier>();

                var res = new UKHitResult(
                    hit.point, hit.normal, hit.transform,
                    new UKProxyEnemy(enemyIdentifier), 
                    new UKProxyProjectile(hit.transform.GetComponentInChildren<Projectile>()));
                return res;
            }

            return null;
        }

        public UKHitResult Linecast(Vector3 start, Vector3 end, int layerMask = DefaultCastMask)
        {
            var diff = end - start;
            return Raycast(start, diff.normalized, diff.magnitude, DefaultCastMask);
        }

        public int CreateLayerMask(params string[] maskNames)
        {
            int res = 0;

            foreach(var name in maskNames)
            {
                res |= (1 << LayerMask.NameToLayer(name));
            }

            return res;
        }

        public int CreateLayerMask(params int[] maskNums)
        {
            int res = 0;
            foreach(var layer in maskNums)
            {
                res |= (1 << layer);
            }

            return res;
        }
    }
}

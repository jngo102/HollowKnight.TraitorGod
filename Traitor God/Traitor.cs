﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using HutongGames.PlayMaker.Actions;
using ModCommon;
using UnityEngine;

namespace Traitor_God
{
    internal class Traitor : MonoBehaviour
    {
        /* Health values */
        private const int Phase1Health = 500;
        public const int Phase2Health = 1000;
        public const int Phase3Health = 1000;
        private const int TotalHealth = Phase1Health + Phase2Health + Phase3Health;

        /* DSlash targeting value */
        private readonly int _dSlashSpeed = 75;

        /* Phase Boolean triggers */
        private bool _enteredPhase2;
        private bool _enteredPhase3;

        /* Color of infection trails */
        public static Color infectionOrange = new Color32(255, 50, 0, 255);

        /* Components */
        public static AudioSource Audio;        // Audio is access by TraitorAudio, so make it public and static
        public static PlayMakerFSM Control;     // Control is accessed by TinkSound, so make it public and static
        private tk2dSpriteAnimator _anim;
        private PlayMakerFSM _gpzControl;
        private HealthManager _hm;
        private Rigidbody2D _rb;
        private Transform _trans;

        /* Trail left behind by Traitor God */
        private ParticleSystem _trail;

        private void Awake()
        {
            _anim = gameObject.GetComponent<tk2dSpriteAnimator>();
            Audio = gameObject.GetComponent<AudioSource>();
            Control = gameObject.LocateMyFSM("Mantis");
            /* Used to spawn shockwaves */
            _gpzControl = TraitorGod.PreloadedGameObjects["GPZ"].LocateMyFSM("Control");
            _hm = gameObject.GetComponent<HealthManager>();
            _rb = gameObject.GetComponent<Rigidbody2D>();
            _trans = GetComponent<Transform>();

            Log("Using alt version: " + PlayerData.instance.statueStateTraitorLord.usingAltVersion);
            Log("Using alt version2: " + PlayerData.instance.GetVariable<BossStatue.Completion>("statueStateTraitor").usingAltVersion);
            if (PlayerData.instance.statueStateTraitorLord.usingAltVersion)
            {
                _hm.hp = TotalHealth;
# if DEBUG
                /* Enter phase 3 immediately */
                _hm.hp = 999;
# endif

                _hm.OnDeath += DeathHandler;
            }
        }

        private IEnumerator Start()
        {
            yield return null;

            while (HeroController.instance == null)
            {
                yield return null;
            }

            if (!PlayerData.instance.statueStateTraitorLord.usingAltVersion)
            {
                /* Revert to old Traitor Lord and slam wave textures if fighting regular Traitor Lord */
                ResetTextures();

                yield break;
            }

            _trail = Trail.AddTrail(gameObject, 4, 0.8f, 1.5f, 2, 1.8f, infectionOrange);

            /* Disable empty walks */
            Control.RemoveTransition("Feint?", "Feint");

            /* Disable slam repeat */
            Control.RemoveTransition("Check L", "Repeat?");
            Control.RemoveTransition("Check R", "Repeat?");
            Control.RemoveTransition("Repeat?", "Repeat");
            Control.RemoveTransition("Repeat", "Too Close?");

            /* Add trail to sickles on Sickle Throw */
            Control.InsertMethod("Sickle Throw Recover", 0, AddSickleTrails);
            /* Target the player during DSlash */
            Control.InsertMethod("DSlash Antic", 0, DSlashTargetPlayer);
            /* Summon two sickles when slashing */
            Control.InsertMethod("Attack Swipe", 0, SpawnSickles(40, 2));
            /* Spawn shockwaves when landing after a DSlash */
            Control.InsertMethod("Land", 0, GroundPound.SpawnShockwaves(transform, _gpzControl, 1, 1, 25, 2));

            /* Loop DSlash animation */
            _anim.GetClipByName("DSlash").wrapMode = tk2dSpriteAnimationClip.WrapMode.Loop;

            Log("Changing to New Sprites");
            /* Change sprites using Mola's Traitor God sprite sheets */
            gameObject.GetComponent<tk2dSprite>().GetCurrentSpriteDef().material.mainTexture =
                TraitorGod.Sprites[0].texture;
            /* Change color of waves using Mola's waves sprite sheet */
            ChangeWaveSprite(1);

            /* Add new moves to Phase 1 */
            GroundPound.AddGroundPound(Control, _gpzControl, _trail, _anim, _rb, _trans);
            WallFall.AddWallFall(Control, _anim, _rb, _trans);

            /* Miscellaneous */
            ChangeStateValues.ChangeFSMValues(Control);
        }

        /* Transition to Wall Fall state when Traitor God is in the DSlash state,
           encounters a wall or roof, and is off the ground */
        private void OnCollisionEnter2D(Collision2D collision)
        {
            if (Control.ActiveStateName == "DSlash" && collision.collider.gameObject.layer == 8 && transform.position.y > 31.6)
            {
                Control.SetState("Wall Fall");
            }
        }

        private void Update()
        {
            if (PlayerData.instance.statueStateTraitorLord.usingAltVersion)
            {
                /* Hacky method of dividing fight into 3 phases */
                if (_hm.hp < (Phase2Health + Phase3Health) && !_enteredPhase2)
                {
                    Log("Entered Phase 2");
                    _enteredPhase2 = true;
                    DoubleSlam.AddDoubleSlam(Control, _anim, _trans);
                }
                else if (_hm.hp < Phase3Health && !_enteredPhase3)
                {
                    Log("Entered Phase 3");
                    _enteredPhase3 = true;
                    TriSpearThrow.AddTriSpearThrow(Control, _anim, _rb, _trans);
                    ThornPillars.AddThornPillars(Control, _anim, _trans);
                }
            }
        }

        private static void Log(object message) => Modding.Logger.Log($"[Traitor]: " + message);

        public static void ClearGameObjectList(List<GameObject> gameObjectList)
        {
            foreach (GameObject go in gameObjectList)
            {
                Destroy(go);
            }
            gameObjectList.Clear();
        }

        /* Begin Coroutine to retract and destroy any existing thorn pillars upon boss death */
        private void DeathHandler()
        {
            StartCoroutine(ThornPillars.RectractThornPillarsAndDestroy());
        }

        private void ResetTextures()
        {
            gameObject.GetComponent<tk2dSprite>().GetCurrentSpriteDef().material.mainTexture = TraitorGod.Sprites[3].texture;
            ChangeWaveSprite(4);
        }

        /* Add a trail to all GameObjects with the name "Shot Traitor Lord(Clone)" */
        private void AddSickleTrails()
        {
            IEnumerable<GameObject> sickles = FindObjectsOfType<GameObject>().Where(obj => obj.name == "Shot Traitor Lord(Clone)");
            foreach (GameObject sickle in sickles)
            {
                sickle.GetComponent<DamageHero>().damageDealt = 2;
                Trail.AddTrail(sickle, 2, 0.5f, 0.75f, 2, 0, infectionOrange);
            }
        }

        /* Always target the player on DSlash */
        private void DSlashTargetPlayer()
        {
            Vector2 dSlashVector = TriSpearThrow.GetVectorToPlayer(_trans) * _dSlashSpeed; ;

            Control.GetAction<SetVelocity2d>("DSlash").x = dSlashVector.x;
            Control.GetAction<SetVelocity2d>("DSlash").y = dSlashVector.y;
        }

        /* Spawn sickles during slash */
        private Action SpawnSickles(float speed, int damage)
        {
            return () =>
            {
                Quaternion angle = Quaternion.Euler(Vector3.zero);
                Transform trans = transform;
                Vector3 pos = trans.position;
                float x = speed * Math.Sign(trans.localScale.x);

                float[] sicklePositionYs = { pos.y - 2, pos.y };

                foreach (float sicklePosY in sicklePositionYs)
                {
                    GameObject sickle = Instantiate(Control
                        .GetAction<SpawnObjectFromGlobalPool>("Sickle Throw")
                        .gameObject.Value, pos.SetY(sicklePosY), angle);

                    sickle.GetComponent<Rigidbody2D>().velocity = Vector2.right * x;
                    sickle.GetComponent<DamageHero>().damageDealt = damage;
                    sickle.name = "Slash Sickle";   // Differentiate these sickles from the ones thrown in a sinusoid
                    Destroy(sickle.GetComponent<AudioSource>());
                    Destroy(sickle.GetComponent<PlayMakerFSM>());
                    Destroy(sickle, 2);
                }
            };
        }

        /* Change slam wave sprite sheet using index of TraitorGod.SPRITES variable */
        private void ChangeWaveSprite(int spriteIndex)
        {
            Vector3 position = Vector3.zero;
            Quaternion rotation = Quaternion.identity;
            GameObject wave = Instantiate(Control.GetAction<SpawnObjectFromGlobalPool>("Waves").gameObject.Value,
                position, rotation);
            byte[] spriteSheetByteData = TraitorGod.SpriteBytes[spriteIndex];
            wave.GetComponentInChildren<SpriteRenderer>().sprite.texture.LoadImage(spriteSheetByteData);
            Destroy(wave);
        }
    }
}
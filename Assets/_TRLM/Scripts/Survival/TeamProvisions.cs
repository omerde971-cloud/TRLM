using System;
using UnityEngine;

namespace TRLM.Survival
{
    /// <summary>
    /// Shared food/water pool for the whole team (not per-player). Lives on its own small
    /// GameObject in the scene, not on PF_Player. Drain rates are Inspector-tunable rather
    /// than hardcoded so balance can be iterated without touching code.
    /// </summary>
    public class TeamProvisions : MonoBehaviour
    {
        [Header("Starting Amounts")]
        [SerializeField] private float startingFood = 300f;
        [SerializeField] private float startingWater = 300f;
        [SerializeField] private int livingTeamMembers = 5;

        [Header("Drain (per in-game day)")]
        // Balance intent: at default values (300 units / ~50 per day) the starting stock covers
        // roughly 5-6 in-game days before the team needs to resupply. "Day" here is whatever
        // length IWorldTimeSource considers a full day-night cycle, not a real-world day.
        [SerializeField] private float foodDrainPerDay = 50f;
        [SerializeField] private float waterDrainPerDay = 55f;
        [SerializeField] private float secondsPerInGameDay = 1200f; // 20 real minutes/day by default

        private float food;
        private float water;

        public event Action<float> OnFoodChanged;
        public event Action<float> OnWaterChanged;

        public float SharedFood => food;
        public float SharedWater => water;
        public int LivingTeamMembers => livingTeamMembers;

        private void Awake()
        {
            food = startingFood;
            water = startingWater;
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            if (secondsPerInGameDay <= 0f) return;

            float dayFraction = dt / secondsPerInGameDay;
            ConsumeFood(foodDrainPerDay * dayFraction);
            ConsumeWater(waterDrainPerDay * dayFraction);
        }

        public void ConsumeFood(float amount)
        {
            if (amount <= 0f) return;
            food = Mathf.Max(0f, food - amount);
            OnFoodChanged?.Invoke(food);
        }

        public void ConsumeWater(float amount)
        {
            if (amount <= 0f) return;
            water = Mathf.Max(0f, water - amount);
            OnWaterChanged?.Invoke(water);
        }

        public void AddFood(float amount)
        {
            if (amount <= 0f) return;
            food += amount;
            OnFoodChanged?.Invoke(food);
        }

        public void AddWater(float amount)
        {
            if (amount <= 0f) return;
            water += amount;
            OnWaterChanged?.Invoke(water);
        }

        public void SetLivingTeamMembers(int count)
        {
            livingTeamMembers = Mathf.Max(0, count);
        }

        /// <summary>Save/load restore only — direct setters so a load doesn't re-run Awake's
        /// startingFood/startingWater reset after values are restored, and doesn't double-drain
        /// via Consume/Add's additive-only API.</summary>
        public void RestoreProvisions(float foodValue, float waterValue, int living)
        {
            food = Mathf.Max(0f, foodValue);
            water = Mathf.Max(0f, waterValue);
            livingTeamMembers = Mathf.Max(0, living);
            OnFoodChanged?.Invoke(food);
            OnWaterChanged?.Invoke(water);
        }
    }
}

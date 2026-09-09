"""Recalculate the design's economy and boss checkpoints; no Unity simulation."""
from decimal import Decimal, ROUND_CEILING
from pathlib import Path
import hashlib
import json

ROOT = Path(__file__).resolve().parents[1]
SOURCE = ROOT / 'docs/cursor-hunter-game-design-v0.0.3.md'
OUTPUT = ROOT / 'output/pdf/cursor-hunter-balance-validation.json'


def calculate():
    text = SOURCE.read_text(encoding='utf-8')
    costs = []
    for base, growth in [(30, '1.18'), (2000, '1.35'), (80000, '1.35'),
                         (5000000, '1.35'), (200000000, '1.35')]:
        costs.append(sum(int((Decimal(base) * Decimal(growth) ** i)
                             .to_integral_value(rounding=ROUND_CEILING)) for i in range(5)))
    periods = [Decimal('1.5'), Decimal(3), Decimal(4), Decimal(5), Decimal(6), Decimal(10)]
    packs, drops = [1, 3, 6, 10, 16, 20], [3, 7, 15, 30, 60, 140]
    spawn_counts = [int((Decimal(60) / p).to_integral_value(rounding=ROUND_CEILING)) * k
                    for p, k in zip(periods, packs)]
    stages = []
    for i, (pack_multiplier, garnet_multiplier) in enumerate(zip([1, 2, 3, 5, 8, 10], [1, 2, 10, 100, 1000, 5000])):
        count = sum(spawn_counts[:i+1]) * pack_multiplier
        garnet = sum(c * d for c, d in zip(spawn_counts[:i+1], drops[:i+1])) * pack_multiplier * garnet_multiplier
        assert f'{garnet:,}' in text, f'Missing calculated garnet supply: {garnet}'
        stages.append({'stage': i, 'spawn_per_60s': count, 'garnet_ceiling': garnet})
    for value in costs:
        assert f'{value:,}' in text, f'Cost total does not match document: {value}'
    bosses = []
    for i, (hp, attack, hits, rate) in enumerate(zip(
            [1300, 12000, 180000, 18000000, 2700000000],
            [21, 100, 1000, 20000, 1000000], [1, 2, 3, 3, 3], [2, 2, 2, 10, 30])):
        dps = attack * hits * rate * .75
        assert f'{hp:,}' in text
        assert 39 <= hp / dps <= 43
        bosses.append({'boss': i+1, 'hp': hp, 'effective_dps_75pct': dps,
                       'ttk_seconds_75pct': round(hp/dps, 2),
                       'ttk_seconds_50pct': round(hp/(attack*hits*rate*.5), 2)})
    return {'source_sha256': hashlib.sha256(SOURCE.read_bytes()).hexdigest(),
            'status': 'arithmetic_checks_passed',
            'limitations': ['Not a combat simulation', 'No playtest or measured performance',
                            '60% income yield is an assumption; optional upgrades alter pacing'],
            'five_level_cost_totals': costs, 'normal_field_supply': stages,
            'boss_checkpoints': bosses,
            'normal_minutes_at_60pct_income': [round(c/(s['garnet_ceiling']*.6), 2) for c, s in zip(costs, stages)]}


if __name__ == '__main__':
    result = calculate()
    OUTPUT.parent.mkdir(parents=True, exist_ok=True)
    OUTPUT.write_text(json.dumps(result, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
    print(json.dumps(result, ensure_ascii=False, indent=2))

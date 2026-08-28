-- Deduplicate before unique index (keep oldest row per instance spawn).
DELETE FROM pot_cycles
WHERE id NOT IN (
  SELECT MIN(id)
  FROM pot_cycles
  GROUP BY instance_key, pot_fate_id, spawn_at_unix
);

DROP INDEX IF EXISTS idx_pot_cycles_instance_spawn;

CREATE UNIQUE INDEX IF NOT EXISTS idx_pot_cycles_instance_fate_spawn
ON pot_cycles(instance_key, pot_fate_id, spawn_at_unix);

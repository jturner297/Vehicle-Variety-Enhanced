# Copilot Instructions

## General Guidelines
- Prefer better RNG and greater vehicle variety: avoid small fixed queues by tracking recent selections and prioritizing items not recently used; use cryptographic RNG for shuffling to reduce repeatable sequences and enlarge/refill batches. Use Fisher-Yates shuffle to improve RNG further.
- Prefer selecting replacement vehicle models that are not already present in the world and avoid models recently spawned.

## Traffic Management
- TrafficEnhanced should operate in an assistive, conservative mode: target only observed duplicates (on-screen or nearby), avoid widespread replacements, and base deduplication only on observed vehicles. It should track and avoid duplicates based only on vehicles the player actually observes; use the observed set for frequency and spawn blocking.
- Use area cooldowns to prevent replacing many cars in the same area; prefer non-aggressive swap policies.
- Implement tiered selection that prefers models not present in the observed area.
- Maintain spawn memory (TTL) and spawn history to enhance vehicle management.
- Expose presets/config and runtime toggles for testing to allow for better customization and experimentation with traffic management settings.
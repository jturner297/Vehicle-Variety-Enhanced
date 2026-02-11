# Copilot Instructions

## General Guidelines
- Prefer better RNG and greater vehicle variety: avoid small fixed queues by tracking recent selections and prioritizing items not recently used; use cryptographic RNG for shuffling to reduce repeatable sequences and enlarge/refill batches. Use Fisher-Yates shuffle to improve RNG further.
- Prefer selecting replacement vehicle models that are not already present in the world and avoid models recently spawned.
- TrafficEnhanced should focus on ambient traffic deduplication and avoiding spawning duplicates already present in traffic, rather than prioritizing ParkedMP compatibility.
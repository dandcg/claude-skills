namespace EmailArchive.Models;

public enum Tier
{
    Unclassified = 0,
    Excluded = 1,      // Skip entirely
    MetadataOnly = 2,  // Store metadata, don't vectorize
    Vectorize = 3      // Full vectorization
}

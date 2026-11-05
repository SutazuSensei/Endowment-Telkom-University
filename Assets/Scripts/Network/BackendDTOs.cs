using System;
using System.Collections.Generic;
using UnityEngine;

namespace Endowment.Network
{
    // ─────────────────────────────────────────────────────────────────────────
    // Error Envelope & Details
    // ─────────────────────────────────────────────────────────────────────────

    [Serializable]
    public class ApiErrorDetailsDTO
    {
        public string nextFeedAt;
    }

    [Serializable]
    public class ApiErrorDTO
    {
        public string code;
        public string message;
        public ApiErrorDetailsDTO details;
    }

    [Serializable]
    public class ErrorEnvelopeDTO
    {
        public string schemaVersion;
        public string requestId;
        public ApiErrorDTO error;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Health & Meta
    // ─────────────────────────────────────────────────────────────────────────

    [Serializable]
    public class HealthDataDTO
    {
        public string status;
    }

    [Serializable]
    public class HealthEnvelopeDTO
    {
        public string schemaVersion;
        public string requestId;
        public HealthDataDTO data;
    }

    [Serializable]
    public class MetaDataDTO
    {
        public string apiVersion;
        public string schemaVersion;
        public string service;
    }

    [Serializable]
    public class MetaEnvelopeDTO
    {
        public string schemaVersion;
        public string requestId;
        public MetaDataDTO data;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Auth Exchange
    // ─────────────────────────────────────────────────────────────────────────

    [Serializable]
    public class AuthExchangeRequestDTO
    {
        public string token;
    }

    [Serializable]
    public class AuthExchangeDataDTO
    {
        public string accessToken;
        public string tokenType;
        public string expiresAt;
    }

    [Serializable]
    public class AuthExchangeEnvelopeDTO
    {
        public string schemaVersion;
        public string requestId;
        public AuthExchangeDataDTO data;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Fish Models (Personal & Public)
    // ─────────────────────────────────────────────────────────────────────────

    [Serializable]
    public class FishAppearanceDTO
    {
        public string colorCode;
        public string accessoryCode;
    }

    [Serializable]
    public class FishStateDTO
    {
        public int hunger;
        public string mood;
        public bool isActive;
        public string lastFedAt;
    }

    [Serializable]
    public class PersonalFishDTO
    {
        public string id;
        public string speciesCode;
        public string assetCode;
        public string tierCode;
        public string displayName;
        public FishAppearanceDTO appearance;
        public FishStateDTO state;
        public string createdAt;
    }

    [Serializable]
    public class PublicFishDTO
    {
        public string id;
        public string speciesCode;
        public string assetCode;
        public string tierCode;
        public string donorDisplayName;
        public FishAppearanceDTO appearance;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Aquarium Models (Personal, Public, Big Aquarium)
    // ─────────────────────────────────────────────────────────────────────────

    [Serializable]
    public class AquariumOwnerDTO
    {
        public string displayName;
    }

    [Serializable]
    public class AquariumStatisticsDTO
    {
        public int fishCount;
        public string highestTier;
    }

    [Serializable]
    public class PersonalAquariumDataDTO
    {
        public string aquariumId;
        public string publicSlug;
        public string name;
        public AquariumOwnerDTO owner;
        public AquariumStatisticsDTO statistics;
        public List<PersonalFishDTO> fish;
        public int syncCursor;
    }

    [Serializable]
    public class PersonalAquariumEnvelopeDTO
    {
        public string schemaVersion;
        public string requestId;
        public PersonalAquariumDataDTO data;
    }

    [Serializable]
    public class BigAquariumDataDTO
    {
        public string mode;
        public int refreshAfterSeconds;
        public int syncCursor;
        public List<PublicFishDTO> fish;
    }

    [Serializable]
    public class BigAquariumEnvelopeDTO
    {
        public string schemaVersion;
        public string requestId;
        public BigAquariumDataDTO data;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Fish Actions (Feed & Appearance)
    // ─────────────────────────────────────────────────────────────────────────

    [Serializable]
    public class FeedFishDataDTO
    {
        public PersonalFishDTO fish;
        public string nextFeedAt;
    }

    [Serializable]
    public class FeedFishEnvelopeDTO
    {
        public string schemaVersion;
        public string requestId;
        public FeedFishDataDTO data;
    }

    [Serializable]
    public class AppearanceRequestDTO
    {
        public string colorCode;
        public string accessoryCode;
    }

    [Serializable]
    public class AppearanceDataDTO
    {
        public PersonalFishDTO fish;
    }

    [Serializable]
    public class AppearanceEnvelopeDTO
    {
        public string schemaVersion;
        public string requestId;
        public AppearanceDataDTO data;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Mock Endowment API Models (Development & Scenario Testing)
    // ─────────────────────────────────────────────────────────────────────────

    [Serializable]
    public class MockDonationRequestDTO
    {
        public string donorExternalId;
        public string donorName;
        public string donorEmail;
        public bool showName;
        public int amount;
        public string currency = "IDR";
    }

    [Serializable]
    public class MockDonationDataDTO
    {
        public string donationId;
        public string eventId;
        public int amount;
        public string currency;
        public string status;
    }

    [Serializable]
    public class MockDonationEnvelopeDTO
    {
        public string schemaVersion;
        public string requestId;
        public MockDonationDataDTO data;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Contract Mapper (Bridges Backend Enum Codes <-> Unity Client Variants)
    // ─────────────────────────────────────────────────────────────────────────

    public static class BackendContractMapper
    {
        /// <summary>
        /// Converts backend tierCode ('COMMON', 'RARE', 'EPIC', 'LEGENDARY')
        /// to Unity client display tier ('Common', 'Rare', 'Epic', 'Legendary').
        /// </summary>
        public static string ToClientTier(string backendTierCode)
        {
            if (string.IsNullOrEmpty(backendTierCode)) return "Common";

            switch (backendTierCode.Trim().ToUpperInvariant())
            {
                case "COMMON": return "Common";
                case "RARE": return "Rare";
                case "EPIC": return "Epic";
                case "LEGENDARY": return "Legendary";
                default: return "Common";
            }
        }

        /// <summary>
        /// Converts Unity client tier ('Common', 'Rare', 'Epic', 'Legendary')
        /// to Mock API scenario slug ('common', 'rare', 'epic', 'legendary').
        /// </summary>
        public static string ToScenarioSlug(string clientTier)
        {
            if (string.IsNullOrEmpty(clientTier)) return "common";
            string lower = clientTier.Trim().ToLowerInvariant();
            if (lower == "common" || lower == "rare" || lower == "epic" || lower == "legendary")
                return lower;
            return "common";
        }

        /// <summary>
        /// Gets the donation amount in IDR corresponding to each tier.
        /// </summary>
        public static int GetAmountForTier(string tier)
        {
            if (string.IsNullOrEmpty(tier)) return 250000;
            switch (tier.Trim().ToLowerInvariant())
            {
                case "common": return 250000;
                case "rare": return 1000000;
                case "epic": return 10000000;
                case "legendary": return 30000000;
                default: return 250000;
            }
        }

        /// <summary>
        /// Converts backend colorCode ('DEFAULT', 'OCEAN_BLUE', 'CORAL_PINK', 'GOLDEN')
        /// to Unity client colorVariant ('blue', 'red', 'yellow', 'green').
        /// </summary>
        public static string ToClientColorVariant(string backendColorCode)
        {
            if (string.IsNullOrEmpty(backendColorCode)) return "blue";

            switch (backendColorCode.Trim().ToUpperInvariant())
            {
                case "DEFAULT": return "blue";
                case "OCEAN_BLUE": return "blue";
                case "CORAL_PINK": return "red";
                case "GOLDEN": return "yellow";
                default: return backendColorCode.Trim().ToLowerInvariant();
            }
        }

        /// <summary>
        /// Converts Unity client colorVariant ('red', 'blue', 'yellow', 'green')
        /// to valid backend colorCode ('DEFAULT', 'OCEAN_BLUE', 'CORAL_PINK', 'GOLDEN').
        /// </summary>
        public static string ToBackendColorCode(string clientColorVariant)
        {
            if (string.IsNullOrEmpty(clientColorVariant)) return "DEFAULT";

            switch (clientColorVariant.Trim().ToLowerInvariant())
            {
                case "blue":
                case "ocean_blue":
                    return "OCEAN_BLUE";
                case "red":
                case "pink":
                case "coral_pink":
                    return "CORAL_PINK";
                case "yellow":
                case "gold":
                case "golden":
                    return "GOLDEN";
                case "green":
                case "default":
                    return "DEFAULT";
                default:
                    return "DEFAULT";
            }
        }

        /// <summary>
        /// Converts backend accessoryCode ('NONE', 'TELU_CAP', 'GRADUATION_HAT')
        /// to Unity client accessoryId ('', 'hat_cowboy', 'hat_bigtop').
        /// </summary>
        public static string ToClientAccessoryId(string backendAccessoryCode)
        {
            if (string.IsNullOrEmpty(backendAccessoryCode)) return "";

            switch (backendAccessoryCode.Trim().ToUpperInvariant())
            {
                case "NONE":
                    return "";
                case "TELU_CAP":
                    return "hat_cowboy";
                case "GRADUATION_HAT":
                    return "hat_bigtop";
                default:
                    return backendAccessoryCode.Trim().ToLowerInvariant();
            }
        }

        /// <summary>
        /// Converts Unity client accessoryId ('hat_bigtop', 'hat_cowboy', 'glasses_fancy', etc.)
        /// to valid backend accessoryCode ('NONE', 'TELU_CAP', 'GRADUATION_HAT').
        /// </summary>
        public static string ToBackendAccessoryCode(string clientAccessoryId)
        {
            if (string.IsNullOrEmpty(clientAccessoryId)) return "NONE";

            string lower = clientAccessoryId.Trim().ToLowerInvariant();
            if (lower == "none") return "NONE";
            if (lower == "telu_cap") return "TELU_CAP";
            if (lower == "graduation_hat") return "GRADUATION_HAT";

            if (lower.Contains("bigtop") || lower.Contains("unicorn") || lower.Contains("graduation"))
                return "GRADUATION_HAT";

            if (lower.Contains("hat") || lower.Contains("cap") || lower.Contains("glass") || lower.Contains("starfish"))
                return "TELU_CAP";

            return "NONE";
        }
    }
}


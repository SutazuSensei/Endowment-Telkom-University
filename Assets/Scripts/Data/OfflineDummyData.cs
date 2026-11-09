using System;
using System.Collections.Generic;

/// <summary>
/// Centralized Offline Dummy Dataset for the Tel-U Endowment Aquarium.
/// Ensures that when the project is run on any developer's or teammate's PC without the local
/// Node.js/PostgreSQL backend running, all features (Personal Aquarium, Collection, Store,
/// Donation Simulation, and Big Aquarium 15-fish rotation + Search by Name) work with zero errors.
/// </summary>
public static class OfflineDummyData
{
    public const int DEFAULT_STARTING_POINTS = 100000;
    public const string DEFAULT_PLAYER_NAME = "Pandu (Tel-U)";
    public const string DEFAULT_PLAYER_TIER = "Common";
    public const string DEFAULT_PLAYER_COLOR = "red";

    [Serializable]
    public class DummyDonorEntry
    {
        public string id;
        public string ownerName;
        public string tier;
        public string colorVariant;
        public string accessoryId;
    }

    private static readonly DummyDonorEntry[] DefaultDonors = new DummyDonorEntry[]
    {
        new DummyDonorEntry { id = "dummy_01", ownerName = "Andi Pratama",   tier = "Common",    colorVariant = "red",    accessoryId = "" },
        new DummyDonorEntry { id = "dummy_02", ownerName = "Siti Rahma",     tier = "Rare",      colorVariant = "blue",   accessoryId = "hat_bigtop" },
        new DummyDonorEntry { id = "dummy_03", ownerName = "Budi Santoso",   tier = "Epic",      colorVariant = "green",  accessoryId = "hat_cowboy" },
        new DummyDonorEntry { id = "dummy_04", ownerName = "Rudi Hermawan",  tier = "Legendary", colorVariant = "yellow", accessoryId = "glasses_fancy" },
        new DummyDonorEntry { id = "dummy_05", ownerName = "Eka Wijaya",     tier = "Common",    colorVariant = "blue",   accessoryId = "" },
        new DummyDonorEntry { id = "dummy_06", ownerName = "Dewi Lestari",   tier = "Rare",      colorVariant = "red",    accessoryId = "glasses_starfish" },
        new DummyDonorEntry { id = "dummy_07", ownerName = "Fajar Nugroho",  tier = "Epic",      colorVariant = "yellow", accessoryId = "hat_sailor" },
        new DummyDonorEntry { id = "dummy_08", ownerName = "Gita Permata",   tier = "Legendary", colorVariant = "green",  accessoryId = "hat_unicorn" },
        new DummyDonorEntry { id = "dummy_09", ownerName = "Hana Kusuma",    tier = "Common",    colorVariant = "yellow", accessoryId = "" },
        new DummyDonorEntry { id = "dummy_10", ownerName = "Irfan Hakim",    tier = "Rare",      colorVariant = "green",  accessoryId = "glasses_redsand" },
        new DummyDonorEntry { id = "dummy_11", ownerName = "Joko Susilo",    tier = "Epic",      colorVariant = "blue",   accessoryId = "hat_bigtop" },
        new DummyDonorEntry { id = "dummy_12", ownerName = "Kiki Amelia",    tier = "Legendary", colorVariant = "red",    accessoryId = "glasses_fancy" },
        new DummyDonorEntry { id = "dummy_13", ownerName = "Lani Maharani",  tier = "Common",    colorVariant = "green",  accessoryId = "" },
        new DummyDonorEntry { id = "dummy_14", ownerName = "Maya Sari",      tier = "Rare",      colorVariant = "yellow", accessoryId = "hat_cowboy" },
        new DummyDonorEntry { id = "dummy_15", ownerName = "Nico Saputra",   tier = "Epic",      colorVariant = "red",    accessoryId = "glasses_starfish" },
        new DummyDonorEntry { id = "dummy_16", ownerName = "Rian Hidayat",   tier = "Legendary", colorVariant = "blue",   accessoryId = "hat_sailor" },
        new DummyDonorEntry { id = "dummy_17", ownerName = "Sari Puspita",   tier = "Common",    colorVariant = "red",    accessoryId = "" },
        new DummyDonorEntry { id = "dummy_18", ownerName = "Bayu Anggara",   tier = "Rare",      colorVariant = "blue",   accessoryId = "glasses_fancy" },
        new DummyDonorEntry { id = "dummy_19", ownerName = "Dina Mariana",   tier = "Epic",      colorVariant = "green",  accessoryId = "hat_unicorn" },
        new DummyDonorEntry { id = "dummy_20", ownerName = "Reza Rahadian",  tier = "Legendary", colorVariant = "yellow", accessoryId = "hat_bigtop" },
        new DummyDonorEntry { id = "dummy_21", ownerName = "Tono Sudrajat",  tier = "Common",    colorVariant = "blue",   accessoryId = "" },
        new DummyDonorEntry { id = "dummy_22", ownerName = "Putri Ayu",      tier = "Rare",      colorVariant = "red",    accessoryId = "hat_cowboy" },
        new DummyDonorEntry { id = "dummy_23", ownerName = "Agus Salim",     tier = "Epic",      colorVariant = "yellow", accessoryId = "glasses_redsand" },
        new DummyDonorEntry { id = "dummy_24", ownerName = "Nadia Vega",     tier = "Legendary", colorVariant = "green",  accessoryId = "glasses_starfish" },
        new DummyDonorEntry { id = "dummy_25", ownerName = "Dimas Aditya",   tier = "Common",    colorVariant = "yellow", accessoryId = "" },
        new DummyDonorEntry { id = "dummy_26", ownerName = "Rizky Febian",   tier = "Rare",      colorVariant = "green",  accessoryId = "hat_sailor" },
        new DummyDonorEntry { id = "dummy_27", ownerName = "Ayu Tingting",   tier = "Epic",      colorVariant = "blue",   accessoryId = "glasses_fancy" },
        new DummyDonorEntry { id = "dummy_28", ownerName = "Bagas Kara",     tier = "Legendary", colorVariant = "red",    accessoryId = "hat_unicorn" }
    };

    public static List<DummyDonorEntry> GetDefaultDonorPool()
    {
        return new List<DummyDonorEntry>(DefaultDonors);
    }
}


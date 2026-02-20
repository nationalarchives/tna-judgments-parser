namespace UK.Gov.Legislation.Common {

internal static class LegWorkAuthorMapping {

    public static string GetWorkAuthorUri(string legislationClass) {
        if (string.IsNullOrEmpty(legislationClass))
            return null;

        string temp = legislationClass switch {
            "EnglandAct"                            => "legislature/EnglishParliament",
            "GreatBritainAct"                       => "legislature/ParliamentOfGreatBritain",
            "IrelandAct"                            => "legislature/OldIrishParliament",
            "NorthernIrelandAct"                    => "legislature/NorthernIrelandAssembly",
            "NorthernIrelandAssemblyMeasure"        => "legislature/NorthernIrelandAssembly",
            "NorthernIrelandParliamentAct"          => "legislature/NorthernIrelandParliament",
            "NorthernIrelandOrderInCouncil"         => "government/uk",
            "NorthernIrelandDraftOrderInCouncil"    => "government/uk",
            "NorthernIrelandStatutoryRule"          => "government/northern-ireland",
            "NorthernIrelandDraftStatutoryRule"     => "government/northern-ireland",
            "ScottishAct"                           => "legislature/ScottishParliament",
            "ScottishOldAct"                        => "legislature/OldScottishParliament",
            "ScottishStatutoryInstrument"           => "government/scotland",
            "ScottishDraftStatutoryInstrument"      => "government/scotland",
            "UnitedKingdomChurchInstrument"         => "legislature/GeneralSynod",
            "UnitedKingdomChurchMeasure"            => "legislature/GeneralSynod",
            "UnitedKingdomPrivateAct"               => "legislature/UnitedKingdomParliament",
            "UnitedKingdomPublicGeneralAct"         => "legislature/UnitedKingdomParliament",
            "UnitedKingdomLocalAct"                 => "legislature/UnitedKingdomParliament",
            "UnitedKingdomMinisterialOrder"         => "government/uk",
            "UnitedKingdomStatutoryInstrument"      => "government/uk",
            "UnitedKingdomDraftStatutoryInstrument" => "government/uk",
            "WelshAssemblyMeasure"                  => "legislature/NationalAssemblyForWales",
            "WelshNationalAssemblyAct"              => "legislature/NationalAssemblyForWales",
            "WelshStatutoryInstrument"              => "government/wales",
            "WelshDraftStatutoryInstrument"         => "government/wales",
            "UnitedKingdomMinisterialDirection"     => "government/uk",
            "UnitedKingdomStatutoryRuleOrOrder"     => "government/uk",
            "NorthernIrelandStatutoryRuleOrOrder"   => "government/northern-ireland",
            _                                       => null
        };

        return temp is null ? null : "http://www.legislation.gov.uk/id/" + temp;
    }

}

}

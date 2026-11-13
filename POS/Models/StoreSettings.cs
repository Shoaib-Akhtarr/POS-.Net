using System;

namespace POS.Models
{
    public class StoreSettings
    {
        public int Id { get; set; } = 1;
        public string ShopName { get; set; } = "Aizaz Fareed Center & Al-Kuwait Electronics";
        public string Subtitle { get; set; } = "Electric, Sanitary, Hardware, Tile, Paint & Solar System";
        public string Address { get; set; } = "Opp. Major Shabbir Sharif Hospital, Kunjah";
        public string Phone1 { get; set; } = "0336-4994843";
        public string Phone2 { get; set; } = "0327-5898969";
        public string ReceiptFooter { get; set; } = "Thank you for your business!";
        public string PrinterName { get; set; } = "XP-80";
        public string PreferredPrintFormat { get; set; } = "Thermal80mm"; // Thermal80mm or A4
        public bool UseBitmapThermalForUrdu { get; set; } = false;
        public string Language { get; set; } = "English"; // English or Urdu
        public string Theme { get; set; } = "Light"; // Light or Dark
        public string AutoBackupFolder { get; set; } = string.Empty; // e.g. Google Drive / OneDrive / USB path

        // FBR / PRA Fiscal Tax Integration Settings
        public bool IsFbrTaxEnabled { get; set; } = false;
        public string FbrEnvironment { get; set; } = "Sandbox"; // "Sandbox" or "Live"
        public string FbrPosId { get; set; } = "102345";
        public string FbrBearerToken { get; set; } = string.Empty;
        public string FbrNtn { get; set; } = string.Empty;
        public string FbrStrn { get; set; } = string.Empty;
        public decimal FbrDefaultTaxRate { get; set; } = 18.0m;
        public bool FbrAutoSync { get; set; } = true;
        public bool FbrOfflineModeAllowed { get; set; } = true;

        public DateTime UpdatedAt { get; set; } = DateTime.Now;
    }
}


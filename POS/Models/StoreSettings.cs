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
        public DateTime UpdatedAt { get; set; } = DateTime.Now;
    }
}


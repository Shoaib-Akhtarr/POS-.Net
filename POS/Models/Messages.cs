using CommunityToolkit.Mvvm.Messaging.Messages;

namespace POS.Models
{
    // Simple message that indicates domain data or app state has changed
    public class DataChangedMessage : ValueChangedMessage<string>
    {
        public DataChangedMessage(string value) : base(value) { }

        public static DataChangedMessage ProductUpdated => new DataChangedMessage("Product");
        public static DataChangedMessage CustomerUpdated => new DataChangedMessage("Customer");
        public static DataChangedMessage TransactionUpdated => new DataChangedMessage("Transaction");
        public static DataChangedMessage SettingsUpdated => new DataChangedMessage("Settings");
        public static DataChangedMessage SessionChanged => new DataChangedMessage("Session");
    }
}

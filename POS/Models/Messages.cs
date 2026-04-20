using CommunityToolkit.Mvvm.Messaging.Messages;

namespace POS.Models
{
    // Simple message that indicates product data has changed (add/update/delete)
    public class DataChangedMessage : ValueChangedMessage<string>
    {
        public DataChangedMessage(string value) : base(value) { }
        
        public static DataChangedMessage ProductUpdated => new DataChangedMessage("Product");
        public static DataChangedMessage CustomerUpdated => new DataChangedMessage("Customer");
        public static DataChangedMessage TransactionUpdated => new DataChangedMessage("Transaction");
    }
}

using Microsoft.AspNetCore.SignalR;

namespace QL_DatVeMayBay.Hubs
{
    public class SeatHub : Hub
    {
        public async Task JoinFlightRoom(string maChuyenBay)
        {
            if (!string.IsNullOrEmpty(maChuyenBay))
            {
                await Groups.AddToGroupAsync(Context.ConnectionId, $"FLIGHT_{maChuyenBay}");
            }
        }
    }
}
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;

namespace CheckinAPI.Hubs
{
    public class CheckinHub : Hub
    {
        public async Task JoinEventMonitorGroup(int eventId)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, $"event_monitor_{eventId}");
        }

        public async Task LeaveEventMonitorGroup(int eventId)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"event_monitor_{eventId}");
        }
    }
}

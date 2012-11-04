using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CommonTypes
{
    public interface UserInterface
    {
        void AddNewKnownUsers(String name, int port, String ip);
        Dictionary<UserView, int> getKnownUsers();
        void CreateReservation(String description, List<UserView> userList, List<ReservationSlot> slotList, String creator, MasterInterface master);
        ReservationMessage SendReservation(ReservationMessage message, int timetout);
        ReservationMessage ReservationSlotsUpdate(ReservationMessage reservation, int timeout);
        ReservationMessage ProposeSlot(ReservationSlot slot, ReservationMessage reservation, int timeout);
        ReservationMessage CommitReservation(ReservationMessage message, int timeout);
        ReservationMessage TwoPhaseCommit(ReservationMessage message, int timeout);
        void setName(String name, int port);
        String getName();
        void disconnect();
        bool AmIDisconnected();
        void connect(bool reconnect, int port);
        ReservationMessage AbortReservation(ReservationMessage message, int timeout);
        void RemoveKnownUser(String userName);
        void Reconnect(int newPort);
        UserCalendarInformation GetCalendarInfo();
    }
}

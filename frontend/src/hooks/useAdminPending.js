import { useCallback, useEffect, useState } from "react";
import eventApi from "../api/eventApi";
import ticketReturnApi from "../api/ticketReturnApi";
import { governanceApi } from "../api/governanceApi";

const POLL_MS = 60000;

// Số việc đang chờ admin xử lý: sự kiện chờ duyệt, yêu cầu hoãn/đổi lịch, yêu cầu hoàn vé.
// Mỗi con số tải độc lập; lỗi của một nguồn chỉ làm con số đó là null, không làm hỏng trang.
export default function useAdminPending() {
  const [pending, setPending] = useState({ events: null, changes: null, returns: null });

  const refresh = useCallback(async () => {
    const [events, changes, returns] = await Promise.allSettled([
      eventApi.getPending(1, 1),
      governanceApi.allChanges("Pending"),
      ticketReturnApi.reviewQueue("Pending"),
    ]);

    setPending({
      events:
        events.status === "fulfilled"
          ? events.value.data?.data?.totalCount ?? events.value.data?.data?.items?.length ?? 0
          : null,
      changes:
        changes.status === "fulfilled" && Array.isArray(changes.value.data?.data)
          ? changes.value.data.data.length
          : null,
      returns:
        returns.status === "fulfilled" && Array.isArray(returns.value.data?.data)
          ? returns.value.data.data.length
          : null,
    });
  }, []);

  useEffect(() => {
    refresh();
    const timer = window.setInterval(refresh, POLL_MS);
    return () => window.clearInterval(timer);
  }, [refresh]);

  return { ...pending, refresh };
}

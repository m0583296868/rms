export class RequestItem {
  id: number = 0;
  title: string = '';
  organizationName: string = '';
  status: string = '';
  priority: string = '';
  assignedTo: string = '';
  createdAt: string = '';
  updatedAt: string = '';
  version: number = 0;
  draftStatus: string = '';
}

export class PagedRequests {
  items: RequestItem[] = [];
  totalCount: number = 0;
  page: number = 1;
  pageSize: number = 20;
}

export class CountItem {
  key: string = '';
  count: number = 0;
}

export class Summary {
  total: number = 0;
  byStatus: CountItem[] = [];
  byPriority: CountItem[] = [];
}

export class StatusHistoryItem {
  id: number = 0;
  requestId: number = 0;
  previousStatus: string = '';
  newStatus: string = '';
  changedAt: string = '';
  changedBy: string = '';
}

export class BulkResult {
  succeeded: RequestItem[] = [];
  failed: { id: number; reason: string }[] = [];
}

export class RequestQuery {
  page: number = 1;
  pageSize: number = 20;
  status: string = '';
  priority: string = '';
  organizationName: string = '';
  assignedTo: string = '';
  from: string = '';
  to: string = '';
  search: string = '';
  sortBy: string = 'createdAt';
  sortDir: string = 'desc';
}

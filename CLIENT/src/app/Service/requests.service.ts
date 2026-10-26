import { inject, Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { BulkResult, PagedRequests, RequestItem, RequestQuery, StatusHistoryItem, Summary } from '../Models/request.model';

@Injectable({
  providedIn: 'root'
})
export class RequestsService {
  http: HttpClient = inject(HttpClient);
  BASE_URL = 'http://localhost:5080/api/Request';

  getPage(query: RequestQuery): Observable<PagedRequests> {
    let params = new HttpParams()
      .set('page', query.page)
      .set('pageSize', query.pageSize)
      .set('sortBy', query.sortBy)
      .set('sortDir', query.sortDir);

    if (query.status) params = params.set('status', query.status);
    if (query.priority) params = params.set('priority', query.priority);
    if (query.organizationName) params = params.set('organizationName', query.organizationName);
    if (query.assignedTo) params = params.set('assignedTo', query.assignedTo);
    if (query.from) params = params.set('from', query.from);
    if (query.to) params = params.set('to', query.to);
    if (query.search) params = params.set('search', query.search);

    return this.http.get<PagedRequests>(this.BASE_URL, { params });
  }

  summary(): Observable<Summary> {
    return this.http.get<Summary>(this.BASE_URL + '/summary');
  }

  history(id: number): Observable<StatusHistoryItem[]> {
    return this.http.get<StatusHistoryItem[]>(this.BASE_URL + '/' + id + '/history');
  }

  updateStatus(id: number, body: { status: string; version: number; changedBy: string }): Observable<RequestItem> {
    return this.http.put<RequestItem>(this.BASE_URL + '/' + id + '/status', body);
  }

  bulk(body: { changedBy: string; items: { id: number; status: string; version: number }[] }): Observable<BulkResult> {
    return this.http.post<BulkResult>(this.BASE_URL + '/bulk-status', body);
  }

  seed(count: number): Observable<{ count: number }> {
    return this.http.post<{ count: number }>(this.BASE_URL + '/seed', { count, reset: true });
  }
}

import { Component, DestroyRef, inject, OnInit } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { CommonModule } from '@angular/common';
import { FormControl, FormsModule, ReactiveFormsModule } from '@angular/forms';
import { HttpErrorResponse } from '@angular/common/http';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatPaginatorModule, PageEvent } from '@angular/material/paginator';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSelectModule } from '@angular/material/select';
import { MatTableModule } from '@angular/material/table';
import { catchError, debounceTime, distinctUntilChanged, EMPTY, Subject, switchMap } from 'rxjs';
import { RequestItem, RequestQuery, Summary } from '../../../Models/request.model';
import { RequestsService } from '../../../Service/requests.service';
import { RequestHistoryComponent } from '../request-history/request-history.component';

@Component({
  selector: 'app-list-requests',
  imports: [
    CommonModule, FormsModule, ReactiveFormsModule, MatButtonModule, MatCardModule,
    MatCheckboxModule, MatFormFieldModule, MatIconModule, MatInputModule,
    MatPaginatorModule, MatProgressSpinnerModule, MatSelectModule, MatTableModule,
    RequestHistoryComponent
  ],
  standalone: true,
  templateUrl: './list-requests.component.html',
  styleUrl: './list-requests.component.css'
})
export class ListRequestsComponent implements OnInit {
  srv: RequestsService = inject(RequestsService);
  private destroyRef = inject(DestroyRef);
  private loadRequests$ = new Subject<void>();

  columns = ['select', 'id', 'title', 'organization', 'status', 'priority', 'assigned','createdAt', 'updated', 'actions'];
  statuses = ['New', 'InProgress', 'Waiting', 'Completed'];
  priorities = ['Low', 'Medium', 'High'];
  sortFields = [
    { value: 'createdAt', label: 'תאריך יצירה' },
    { value: 'updatedAt', label: 'תאריך עדכון' },
    { value: 'title', label: 'כותרת' },
    { value: 'organizationName', label: 'ארגון' },
    { value: 'priority', label: 'עדיפות' },
    { value: 'status', label: 'סטטוס' }
  ];

  query = new RequestQuery();
  searchControl = new FormControl('', { nonNullable: true });
  rows: RequestItem[] = [];
  total = 0;
  loading = false;
  error = '';
  actionError = '';
  conflictId = 0;
  summary: Summary | null = null;
  selected = new Set<number>();
  bulkStatus = 'InProgress';
  bulkMessage = '';
  openHistoryId = 0;
  changedBy = '';
  seedCount = 100000;
  seeding = false;

  status = '';
  priority = '';
  organizationName = '';
  assignedTo = '';
  from = '';
  to = '';
  sortBy = 'createdAt';
  sortDir = 'desc';

  ngOnInit() {
    this.changedBy = localStorage.getItem('changedBy') ?? '';

    this.searchControl.valueChanges.pipe(
      debounceTime(300),
      distinctUntilChanged(),
      takeUntilDestroyed(this.destroyRef)
    ).subscribe(value => {
      this.query.search = value.trim();
      this.query.page = 1;
      this.loadRequests$.next();
    });

    this.loadRequests$.pipe(
      switchMap(() => {
        this.loading = true;
        this.error = '';
        return this.srv.getPage(this.query).pipe(
          catchError((err: HttpErrorResponse) => {
            this.loading = false;
            this.error = this.readError(err);
            this.rows = [];
            this.total = 0;
            return EMPTY;
          })
        );
      }),
      takeUntilDestroyed(this.destroyRef)
    ).subscribe(page => {
      this.loading = false;
      this.total = page.totalCount;
      this.rows = page.items.map(item => ({
        ...item,
        draftStatus: this.nextStatuses(item.status)[0] ?? ''
      }));
      this.selected.clear();
    });

    this.loadRequests$.next();
    this.loadSummary();
  }

  applyFilters() {
    this.query.page = 1;
    this.query.status = this.status;
    this.query.priority = this.priority;
    this.query.organizationName = this.organizationName.trim();
    this.query.assignedTo = this.assignedTo.trim();
    this.query.from = this.from;
    this.query.to = this.to;
    this.query.sortBy = this.sortBy;
    this.query.sortDir = this.sortDir;
    this.loadRequests$.next();
  }

  clearFilters() {
    this.status = '';
    this.priority = '';
    this.organizationName = '';
    this.assignedTo = '';
    this.from = '';
    this.to = '';
    this.sortBy = 'createdAt';
    this.sortDir = 'desc';
    this.query.search = '';
    this.searchControl.setValue('', { emitEvent: false });
    this.applyFilters();
  }

  onPage(event: PageEvent) {
    this.query.page = event.pageIndex + 1;
    this.query.pageSize = event.pageSize;
    this.loadRequests$.next();
  }

  reload() {
    this.conflictId = 0;
    this.loadRequests$.next();
    this.loadSummary();
  }

  saveUser() {
    localStorage.setItem('changedBy', this.changedBy);
  }

  toggle(row: RequestItem, checked: boolean) {
    if (checked) this.selected.add(row.id);
    else this.selected.delete(row.id);
  }

  saveStatus(row: RequestItem) {
    if (!this.changedBy.trim()) {
      this.actionError = 'יש להזין מי מבצע את העדכון';
      return;
    }
    this.actionError = '';
    this.srv.updateStatus(row.id, {
      status: row.draftStatus,
      version: row.version,
      changedBy: this.changedBy.trim()
    }).subscribe({
      next: updated => {
        row.status = updated.status;
        row.version = updated.version;
        row.updatedAt = updated.updatedAt;
        row.draftStatus = this.nextStatuses(updated.status)[0] ?? '';
        this.conflictId = 0;
        this.loadSummary();
      },
      error: (err: HttpErrorResponse) => {
        if (err.status === 409) this.conflictId = row.id;
        else this.actionError = this.readError(err);
      }
    });
  }

  runBulk() {
    if (!this.changedBy.trim()) {
      this.actionError = 'יש להזין מי מבצע את העדכון';
      return;
    }
    const items = this.rows
      .filter(row => this.selected.has(row.id))
      .map(row => ({ id: row.id, status: this.bulkStatus, version: row.version }));

    if (items.length === 0) {
      this.actionError = 'יש לסמן פניות לעדכון';
      return;
    }
    if (items.length > 100) {
      this.actionError = 'ניתן לעדכן עד 100 פניות בבת אחת';
      return;
    }

    this.srv.bulk({ changedBy: this.changedBy.trim(), items }).subscribe({
      next: result => {
        this.bulkMessage = `עודכנו ${result.succeeded.length}, נכשלו ${result.failed.length}`;
        this.reload();
      },
      error: (err: HttpErrorResponse) => this.actionError = this.readError(err)
    });
  }

  toggleHistory(id: number) {
    this.openHistoryId = this.openHistoryId === id ? 0 : id;
  }

  seed() {
    this.seeding = true;
    this.error = '';
    this.srv.seed(this.seedCount).subscribe({
      next: () => {
        this.seeding = false;
        this.reload();
      },
      error: (err: HttpErrorResponse) => {
        this.seeding = false;
        this.error = this.readError(err);
      }
    });
  }

  nextStatuses(current: string) {
    const allowed: Record<string, string[]> = {
      New: ['InProgress', 'Waiting'],
      InProgress: ['Waiting', 'Completed'],
      Waiting: ['InProgress', 'Completed'],
      Completed: []
    };
    return allowed[current] ?? [];
  }

  labelStatus(status: string) {
    const names: Record<string, string> = {
      New: 'חדש', InProgress: 'בטיפול', Waiting: 'ממתין', Completed: 'הושלם'
    };
    return names[status] ?? status;
  }

  labelPriority(priority: string) {
    const names: Record<string, string> = { Low: 'נמוכה', Medium: 'בינונית', High: 'גבוהה' };
    return names[priority] ?? priority;
  }

  private loadSummary() {
    this.srv.summary().subscribe({
      next: summary => this.summary = summary,
      error: () => this.summary = null
    });
  }

  private readError(err: HttpErrorResponse) {
    const detail = err.error?.detail;
    return typeof detail === 'string' ? detail : 'שגיאה בתקשורת עם השרת';
  }
}

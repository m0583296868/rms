import { Component, inject, Input, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { RequestsService } from '../../../Service/requests.service';
import { StatusHistoryItem } from '../../../Models/request.model';
import { IsraelTimePipe } from '../../../israel-time.pipe';

@Component({
  selector: 'app-request-history',
  imports: [CommonModule, MatProgressSpinnerModule, IsraelTimePipe],
  standalone: true,
  templateUrl: './request-history.component.html',
  styleUrl: './request-history.component.css'
})
export class RequestHistoryComponent implements OnInit {
  @Input() requestId = 0;

  srv: RequestsService = inject(RequestsService);
  rows: StatusHistoryItem[] = [];
  loading = true;
  error = '';

  ngOnInit() {
    this.srv.history(this.requestId).subscribe({
      next: rows => {
        this.rows = rows;
        this.loading = false;
      },
      error: () => {
        this.loading = false;
        this.error = 'לא ניתן לטעון את היסטוריית השינויים';
      }
    });
  }

  label(status: string) {
    const names: Record<string, string> = {
      New: 'חדש',
      InProgress: 'בטיפול',
      Waiting: 'ממתין',
      Completed: 'הושלם'
    };
    return names[status] ?? status;
  }
}

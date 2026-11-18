import { CommonModule } from '@angular/common';
import { ChangeDetectorRef, Component, DestroyRef, OnInit, inject } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { ExpenseService } from '../expenses/expense.service';
import { SpendingSummary } from '../expenses/expense.types';

@Component({
  selector: 'app-dashboard',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './dashboard.html',
  styleUrl: './dashboard.scss',
})
export class DashboardComponent implements OnInit {
  spendingSummary: SpendingSummary[] = [];
  isLoading = true;
  errorMessage = '';

  private readonly destroyRef = inject(DestroyRef);

  constructor(
    private readonly expenseService: ExpenseService,
    private readonly cdr: ChangeDetectorRef,
  ) {}

  ngOnInit(): void {
    this.loadSpendingSummary();
  }

  private loadSpendingSummary(): void {
    this.expenseService
      .getSpendingSummary()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (summary) => {
          this.spendingSummary = summary;
          this.isLoading = false;
          this.cdr.detectChanges();
        },
        error: () => {
          this.errorMessage = 'Failed to load the spending summary. Please check if the API is running.';
          this.isLoading = false;
          this.cdr.detectChanges();
        },
      });
  }
}

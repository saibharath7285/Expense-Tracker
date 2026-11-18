import { ChangeDetectorRef, Component, DestroyRef, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router } from '@angular/router';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { ExpenseService } from '../expense.service';
import { Expense } from '../expense.types';

@Component({
  selector: 'app-list',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './list.html',
  styleUrl: './list.scss',
})
export class ListComponent implements OnInit {
  expenses: Expense[] = [];
  isLoading = true;
  isDeleting = false;
  errorMessage = '';
  deleteErrorMessage = '';
  expenseToDelete: Expense | null = null;

  private readonly destroyRef = inject(DestroyRef);

  constructor(
    private readonly expenseService: ExpenseService,
    private readonly router: Router,
    private readonly cdr: ChangeDetectorRef,
  ) {}

  ngOnInit(): void {
    this.loadExpenses();
  }

  loadExpenses(): void {
    this.isLoading = true;
    this.errorMessage = '';

    this.expenseService
      .getExpenses()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (expenses) => {
          this.expenses = expenses;
          this.isLoading = false;
          this.cdr.detectChanges();
        },
        error: () => {
          this.errorMessage = 'Failed to load expenses. Please check if the API is running.';
          this.isLoading = false;
          this.cdr.detectChanges();
        },
      });
  }

  addExpense(): void {
    void this.router.navigate(['/expenses/new']);
  }

  viewExpense(expense: Expense): void {
    void this.router.navigate(['/expenses', expense.id, 'view']);
  }

  editExpense(expense: Expense): void {
    void this.router.navigate(['/expenses', expense.id, 'edit']);
  }

  requestDelete(expense: Expense): void {
    this.expenseToDelete = expense;
    this.deleteErrorMessage = '';
  }

  cancelDelete(): void {
    if (this.isDeleting) {
      return;
    }

    this.expenseToDelete = null;
    this.deleteErrorMessage = '';
  }

  confirmDelete(): void {
    if (!this.expenseToDelete || this.isDeleting) {
      return;
    }

    this.isDeleting = true;
    this.deleteErrorMessage = '';

    this.expenseService
      .deleteExpense(this.expenseToDelete.id)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: () => {
          this.expenseToDelete = null;
          this.isDeleting = false;
          this.loadExpenses();
        },
        error: () => {
          this.deleteErrorMessage = 'The expense could not be deleted. Please try again.';
          this.isDeleting = false;
          this.cdr.detectChanges();
        },
      });
  }
}

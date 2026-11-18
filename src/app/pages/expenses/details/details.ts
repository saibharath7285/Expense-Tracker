import { CommonModule } from '@angular/common';
import { ChangeDetectorRef, Component, DestroyRef, OnInit, inject } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { forkJoin } from 'rxjs';
import { ExpenseService } from '../expense.service';
import { Category, Expense, ExpenseRequest } from '../expense.types';

type DetailMode = 'create' | 'edit' | 'view';

@Component({
  selector: 'app-details',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule],
  styleUrl: './details.scss',
  templateUrl: './details.html',
})
export class Details implements OnInit {
  categories: Category[] = [];
  expenseId: number | null = null;
  mode: DetailMode = 'create';
  isLoading = true;
  isSaving = false;
  errorMessage = '';

  private readonly formBuilder = inject(FormBuilder);
  private readonly destroyRef = inject(DestroyRef);

  readonly form = this.formBuilder.group({
    title: ['', [Validators.required, Validators.maxLength(200)]],
    categoryId: [null as number | null, Validators.required],
    subCategoryId: [null as number | null, Validators.required],
    amount: [null as number | null, [Validators.required, Validators.min(0.01)]],
    expenseDate: ['', Validators.required],
    notes: ['', Validators.maxLength(1000)],
  });

  constructor(
    private readonly expenseService: ExpenseService,
    private readonly route: ActivatedRoute,
    private readonly router: Router,
    private readonly cdr: ChangeDetectorRef,
  ) {}

  ngOnInit(): void {
    this.mode = (this.route.snapshot.data['mode'] as DetailMode | undefined) ?? 'create';
    const routeId = this.route.snapshot.paramMap.get('id');
    this.expenseId = routeId === null ? null : Number(routeId);

    if (this.mode !== 'create' && (!this.expenseId || this.expenseId <= 0)) {
      this.errorMessage = 'The requested expense could not be found.';
      this.isLoading = false;
      this.cdr.detectChanges();
      return;
    }

    this.loadDetail();
  }

  get isReadOnly(): boolean {
    return this.mode === 'view';
  }

  get pageTitle(): string {
    if (this.mode === 'create') {
      return 'Add Expense';
    }

    return this.mode === 'view' ? 'View Expense' : 'Edit Expense';
  }

  get pageDescription(): string {
    if (this.mode === 'create') {
      return 'Record a new expense.';
    }

    return this.mode === 'view' ? 'Review this expense record.' : 'Update this expense record.';
  }

  get categoryOptions(): { id: number; name: string }[] {
    return Array.from(
      new Map(this.categories.map((category) => [category.categoryId, category.categoryName])).entries(),
    ).map(([id, name]) => ({ id, name }));
  }

  get subCategoryOptions(): Category[] {
    const categoryId = this.form.controls.categoryId.value;
    return this.categories.filter(
      (category) => category.categoryId === categoryId && category.subCategoryId !== null,
    );
  }

  loadDetail(): void {
    this.isLoading = true;
    this.errorMessage = '';

    if (this.mode === 'create') {
      this.expenseService
        .getCategories()
        .pipe(takeUntilDestroyed(this.destroyRef))
        .subscribe({
          next: (categories) => {
            this.categories = categories;
            this.isLoading = false;
            this.cdr.detectChanges();
          },
          error: () => this.setLoadError(),
        });
      return;
    }

    forkJoin({
      categories: this.expenseService.getCategories(),
      expenses: this.expenseService.getExpenses(),
    })
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: ({ categories, expenses }) => this.populateExistingExpense(categories, expenses),
        error: () => this.setLoadError(),
      });
  }

  onCategoryChange(): void {
    const currentSubCategoryId = this.form.controls.subCategoryId.value;
    const subCategoryStillAvailable = this.subCategoryOptions.some(
      (category) => category.subCategoryId === currentSubCategoryId,
    );

    if (!subCategoryStillAvailable) {
      this.form.controls.subCategoryId.setValue(this.subCategoryOptions[0]?.subCategoryId ?? null);
    }
  }

  save(): void {
    if (this.isReadOnly || this.isSaving) {
      return;
    }

    this.errorMessage = '';
    this.form.markAllAsTouched();
    if (this.form.invalid) {
      return;
    }

    const formValue = this.form.getRawValue();
    const expense: ExpenseRequest = {
      title: formValue.title!.trim(),
      categoryId: formValue.categoryId!,
      subCategoryId: formValue.subCategoryId!,
      amount: formValue.amount!,
      expenseDate: formValue.expenseDate!,
      notes: formValue.notes?.trim() || null,
    };

    this.isSaving = true;
    const request = this.mode === 'create'
      ? this.expenseService.addExpense(expense)
      : this.expenseService.updateExpense(this.expenseId!, expense);

    request.pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: () => void this.router.navigate(['/expenses']),
      error: () => {
        this.errorMessage = `The expense could not be ${this.mode === 'create' ? 'added' : 'updated'}. Please try again.`;
        this.isSaving = false;
        this.cdr.detectChanges();
      },
    });
  }

  goBack(): void {
    void this.router.navigate(['/expenses']);
  }

  private populateExistingExpense(categories: Category[], expenses: Expense[]): void {
    const expense = expenses.find((item) => item.id === this.expenseId);
    if (!expense) {
      this.errorMessage = 'The requested expense could not be found.';
      this.isLoading = false;
      this.cdr.detectChanges();
      return;
    }

    this.categories = categories;
    this.form.patchValue({
      title: expense.title,
      categoryId: expense.categoryId,
      subCategoryId: expense.subCategoryId,
      amount: expense.amount,
      expenseDate: expense.expenseDate.slice(0, 10),
      notes: expense.notes ?? '',
    });

    if (this.isReadOnly) {
      this.form.disable();
    }

    this.isLoading = false;
    this.cdr.detectChanges();
  }

  private setLoadError(): void {
    this.errorMessage = 'Failed to load expense details. Please check if the API is running.';
    this.isLoading = false;
    this.cdr.detectChanges();
  }
}

using FluentValidation;
using HomeMaintenance.Application.Customers.Dtos;
using HomeMaintenance.Domain.Repositories;
using Microsoft.Extensions.Localization;
using System;
using System.Collections.Generic;
using System.Text;

namespace HomeMaintenance.Application.Customers.Validations
{
    public class CustomerValidator : AbstractValidator<CreateUpdateCustomerDto>
    {
        private readonly ICustomerRepository _customerRepository;

        public CustomerValidator(ICustomerRepository customerRepository)
        {
            _customerRepository = customerRepository;

            RuleFor(x => x.Name)
                .NotEmpty().WithMessage("Name is required.")
                .MaximumLength(100).WithMessage("Name cannot exceed 100 characters.");
            
            RuleFor(x => x.Email)
                .NotEmpty().WithMessage("Email is required.")
                .EmailAddress().WithMessage("Email is invalid.");

            RuleFor(x => x.PhoneNumber)
                .NotEmpty().WithMessage("Phone number is required.")
                .Matches(@"^\+?\d{2,15}$").WithMessage("Phone number is invalid.");

            RuleFor(x => x.DefaultAddress)
                .NotEmpty().WithMessage("Default address is required.")
                .MaximumLength(200).WithMessage("Default address cannot exceed 200 characters.");
        }

    }
}
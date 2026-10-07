using Azure.Core;
using Carter;
using KYS.Context;
using KYS.DTOS;
using KYS.Models;
using Mapster;
using Microsoft.EntityFrameworkCore;
using TS.Result;
using KYS.Services;

namespace KYS.Modules;

public sealed class User01Module : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder group)
    {
        var app =group.MapGroup("/users").WithTags("Users");
        app.MapGet(string.Empty, async (
            ApplicationDbContext dbContext,
            CancellationToken cancellationToken) =>
       {
           var result = await dbContext.User01.OrderBy(x => x.UserName)
               .ProjectToType<User01ResponseDTO>().ToListAsync(cancellationToken);
           return Result<List<User01ResponseDTO>>.Succeed(result);
       }).Produces<Result<List<User01ResponseDTO>>>();



        app.MapGet("/{id:Guid}", async (
            Guid id,
            ApplicationDbContext dbContext,
            CancellationToken cancellationToken) =>
        {
            var result = await dbContext.User01.FirstOrDefaultAsync(x => x.Id== id, cancellationToken);
            if (result is null)
                return Results.NotFound(Result<User01ResponseDTO>.Failure("Kullanıcı Bulunamadı"));
            return Results.Ok(Result<User01ResponseDTO>.Succeed(result.Adapt<User01ResponseDTO>()));
        }).Produces<Result<User01ResponseDTO>>().WithName("GetUserById");

        app.MapPost(string.Empty, async (
            User01CreateDTO request,
            ApplicationDbContext dbContext,
            UserPasswordService passwords,
            CancellationToken cancellationToken) =>
        {
            if (string.IsNullOrWhiteSpace(request.UserName) || string.IsNullOrEmpty(request.Password))
                return Results.BadRequest(Result<string>.Failure("Kullanıcı adı ve şifre zorunludur"));
            bool isValidUserName = await dbContext.User01.AnyAsync(x => x.UserName == request.UserName, cancellationToken);
            if (isValidUserName)
            {
                var res =  Result<string>.Failure("Kullanıcı Adı Zaten Kullanılıyor");
                return Results.BadRequest(res);
            }
            var user01 = request.Adapt<User01>();
            user01.Password = passwords.Hash(user01, request.Password);
            dbContext.User01.Add(user01);
            await dbContext.SaveChangesAsync(cancellationToken);
            return Results.Ok(Result<string>.Succeed("Kullanıcı Başarıyla Eklendi"));
        }).Produces<Result<string>>().WithName("CreateUser");

        app.MapPut(string.Empty, async (
            Guid id,
            User01UpdateDTO request,
            ApplicationDbContext dbContext,
            UserPasswordService passwords,
            CancellationToken cancellationToken) =>
        {
            if (string.IsNullOrWhiteSpace(request.UserName) || string.IsNullOrEmpty(request.Password))
                return Results.BadRequest(Result<string>.Failure("Kullanıcı adı ve şifre zorunludur"));
            var user01 = await dbContext.User01.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
            if (user01 is null)
                return Results.NotFound(Result<string>.Failure("Kullanıcı Bulunamadı"));
            bool isValidUserName = await dbContext.User01.AnyAsync(x => x.UserName == request.UserName && x.Id != id, cancellationToken);
            if (isValidUserName)
                return Results.BadRequest(Result<string>.Failure("Kullanıcı Adı Zaten Kullanılıyor"));
            
            user01 = request.Adapt(user01);
            user01.Password = passwords.Hash(user01, request.Password);
            await dbContext.SaveChangesAsync(cancellationToken);
            return Results.Ok(Result<string>.Succeed("Kullanıcı Başarıyla Güncellendi"));
        }).Produces<Result<string>>().WithName("UpdateUser");

        app.MapDelete("/{id:Guid}", async (
            Guid id,
            ApplicationDbContext dbContext,
            CancellationToken cancellationToken) =>
        {
            var result = await dbContext.User01.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
            if (result is null)
                return Results.NotFound(Result<string>.Failure("Kullanıcı Bulunamadı"));
            dbContext.User01.Remove(result);
            await dbContext.SaveChangesAsync(cancellationToken);
            return Results.Ok(Result<string>.Succeed("Kullanıcı Başarıyla Silindi"));
        }).Produces<Result<string>>().WithName("DeleteUser");
    }
}

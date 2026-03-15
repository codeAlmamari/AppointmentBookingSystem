# Stage 1: Base runtime image
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
USER root

# Fix libgssapi warning + create uploads folder
RUN apt-get update && apt-get install -y --no-install-recommends \
    libgssapi-krb5-2 \
    && rm -rf /var/lib/apt/lists/* \
    && mkdir -p /app/uploads

WORKDIR /app
EXPOSE 8080
EXPOSE 8081

# Stage 2: Build
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

COPY ["AppointmentBookingSystem.csproj", "."]
RUN dotnet restore "./AppointmentBookingSystem.csproj"

COPY . .
WORKDIR "/src/."
RUN dotnet build "./AppointmentBookingSystem.csproj" -c $BUILD_CONFIGURATION -o /app/build

# Stage 3: Publish
FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./AppointmentBookingSystem.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

# Stage 4: Final
FROM base AS final
WORKDIR /app

COPY --from=publish /app/publish .

# Copy seed data into the image
COPY seed/ ./seed/

ENV ASPNETCORE_HTTP_PORTS=8080

ENTRYPOINT ["dotnet", "AppointmentBookingSystem.dll"]
from http import HTTPStatus
from typing import Any, cast
from urllib.parse import quote

import httpx2

from ...client import AuthenticatedClient, Client
from ...types import Response, UNSET
from ... import errors

from ...models.problem_details import ProblemDetails
from ...models.viewport_cast import ViewportCast
from ...models.viewport_cast_request import ViewportCastRequest
from typing import cast



def _get_kwargs(
    *,
    body: ViewportCastRequest,

) -> dict[str, Any]:
    headers: dict[str, Any] = {}


    

    

    _kwargs: dict[str, Any] = {
        "method": "post",
        "url": "/api/v1/viewport/cast",
    }

    _kwargs["json"] = body.to_dict()

    headers["Content-Type"] = "application/json"

    _kwargs["headers"] = headers
    return _kwargs



def _parse_response(*, client: AuthenticatedClient | Client, response: httpx2.Response) -> ProblemDetails | ViewportCast | None:
    if response.status_code == 200:
        response_200 = ViewportCast.from_dict(response.json())



        return response_200

    if response.status_code == 400:
        response_400 = ProblemDetails.from_dict(response.json())



        return response_400

    if response.status_code == 409:
        response_409 = ProblemDetails.from_dict(response.json())



        return response_409

    if response.status_code == 503:
        response_503 = ProblemDetails.from_dict(response.json())



        return response_503

    if client.raise_on_unexpected_status:
        raise errors.UnexpectedStatus(response.status_code, response.content)
    else:
        return None


def _build_response(*, client: AuthenticatedClient | Client, response: httpx2.Response) -> Response[ProblemDetails | ViewportCast]:
    return Response(
        status_code=HTTPStatus(response.status_code),
        content=response.content,
        headers=response.headers,
        parsed=_parse_response(client=client, response=response),
    )


def sync_detailed(
    *,
    client: AuthenticatedClient | Client,
    body: ViewportCastRequest,

) -> Response[ProblemDetails | ViewportCast]:
    """ Where rays along one direction meet the open mesh in its own space, such as a decal's surface
    measured along the decal's facing (#263).

    Args:
        body (ViewportCastRequest): Asks where rays along one direction meet the open mesh, in its
            own space and through what the viewport draws
            (#263): a decal's surface measured along the decal's own direction, wherever the camera
            is, in one round trip.

    Raises:
        errors.UnexpectedStatus: If the server returns an undocumented status code and Client.raise_on_unexpected_status is True.
        httpx2.TimeoutException: If the request takes longer than Client.timeout.

    Returns:
        Response[ProblemDetails | ViewportCast]
     """


    kwargs = _get_kwargs(
        body=body,

    )

    response = client.get_httpx_client().request(
        **kwargs,
    )

    return _build_response(client=client, response=response)

def sync(
    *,
    client: AuthenticatedClient | Client,
    body: ViewportCastRequest,

) -> ProblemDetails | ViewportCast | None:
    """ Where rays along one direction meet the open mesh in its own space, such as a decal's surface
    measured along the decal's facing (#263).

    Args:
        body (ViewportCastRequest): Asks where rays along one direction meet the open mesh, in its
            own space and through what the viewport draws
            (#263): a decal's surface measured along the decal's own direction, wherever the camera
            is, in one round trip.

    Raises:
        errors.UnexpectedStatus: If the server returns an undocumented status code and Client.raise_on_unexpected_status is True.
        httpx2.TimeoutException: If the request takes longer than Client.timeout.

    Returns:
        ProblemDetails | ViewportCast
     """


    return sync_detailed(
        client=client,
body=body,

    ).parsed

async def asyncio_detailed(
    *,
    client: AuthenticatedClient | Client,
    body: ViewportCastRequest,

) -> Response[ProblemDetails | ViewportCast]:
    """ Where rays along one direction meet the open mesh in its own space, such as a decal's surface
    measured along the decal's facing (#263).

    Args:
        body (ViewportCastRequest): Asks where rays along one direction meet the open mesh, in its
            own space and through what the viewport draws
            (#263): a decal's surface measured along the decal's own direction, wherever the camera
            is, in one round trip.

    Raises:
        errors.UnexpectedStatus: If the server returns an undocumented status code and Client.raise_on_unexpected_status is True.
        httpx2.TimeoutException: If the request takes longer than Client.timeout.

    Returns:
        Response[ProblemDetails | ViewportCast]
     """


    kwargs = _get_kwargs(
        body=body,

    )

    response = await client.get_async_httpx_client().request(
        **kwargs
    )

    return _build_response(client=client, response=response)

async def asyncio(
    *,
    client: AuthenticatedClient | Client,
    body: ViewportCastRequest,

) -> ProblemDetails | ViewportCast | None:
    """ Where rays along one direction meet the open mesh in its own space, such as a decal's surface
    measured along the decal's facing (#263).

    Args:
        body (ViewportCastRequest): Asks where rays along one direction meet the open mesh, in its
            own space and through what the viewport draws
            (#263): a decal's surface measured along the decal's own direction, wherever the camera
            is, in one round trip.

    Raises:
        errors.UnexpectedStatus: If the server returns an undocumented status code and Client.raise_on_unexpected_status is True.
        httpx2.TimeoutException: If the request takes longer than Client.timeout.

    Returns:
        ProblemDetails | ViewportCast
     """


    return (await asyncio_detailed(
        client=client,
body=body,

    )).parsed
